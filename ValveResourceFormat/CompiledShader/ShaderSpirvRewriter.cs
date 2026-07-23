using System.Buffers.Binary;
using System.IO;
using System.Text;
using Vortice.SpirvCross;

namespace ValveResourceFormat.CompiledShader;

/// <summary>
/// Collected names assigned to SPIR-V identifiers and struct members during reflection.
/// </summary>
public sealed class SpirvNameMap
{
    /// <summary>Maps SPIR-V <c>&lt;id&gt;</c> to the human-readable name to apply via <c>OpName</c>.</summary>
    public Dictionary<uint, string> Names { get; } = [];

    /// <summary>Maps <c>(struct type id, member index)</c> to the name to apply via <c>OpMemberName</c>.</summary>
    public Dictionary<(uint TypeId, uint MemberIndex), string> MemberNames { get; } = [];
}

/// <summary>
/// Rewrites SPIR-V binaries to embed human-readable resource names sourced from shader metadata,
/// producing modules that external tools (RenderDoc, spirv-cross CLI, etc.) display with meaningful symbols.
/// </summary>
public static class ShaderSpirvRewriter
{
    private const uint SpirvMagicLE = 0x07230203;
    private const int SpirvHeaderWordCount = 5;

    // SPIR-V opcodes for sections 1–7 of the logical module layout
    // (https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html#_logical_layout_of_a_module).
    // Anything outside this set marks the start of annotations or later sections, where new debug names
    // are no longer allowed by the spec.
    private const ushort OpSourceContinued = 2;
    private const ushort OpSource = 3;
    private const ushort OpSourceExtension = 4;
    private const ushort OpName = 5;
    private const ushort OpMemberName = 6;
    private const ushort OpString = 7;
    private const ushort OpExtension = 10;
    private const ushort OpExtInstImport = 11;
    private const ushort OpMemoryModel = 14;
    private const ushort OpEntryPoint = 15;
    private const ushort OpExecutionMode = 16;
    private const ushort OpCapability = 17;
    private const ushort OpModuleProcessed = 330;
    private const ushort OpExecutionModeId = 331;

    /// <summary>
    /// Reflects resource names via SPIRV-Cross and rewrites the source binary to embed them as debug instructions.
    /// </summary>
    /// <param name="vulkanSource">The Vulkan shader source containing the SPIR-V bytecode.</param>
    /// <returns>A new SPIR-V byte stream with reflected names embedded.</returns>
    public static byte[] InjectMetadataNames(VfxShaderFileVulkan vulkanSource)
    {
        var nameMap = new SpirvNameMap();
        ShaderSpirvReflection.ReflectSpirv(vulkanSource, Backend.GLSL, out _, nameMap);
        return RewriteOpNames(vulkanSource.Bytecode, nameMap);
    }

    /// <summary>
    /// Rewrites a SPIR-V binary to apply the supplied name overrides. Existing <c>OpName</c>/<c>OpMemberName</c>
    /// entries for the affected ids are dropped and new ones are inserted at the end of the debug-name section.
    /// </summary>
    public static byte[] RewriteOpNames(byte[] spirv, SpirvNameMap nameMap)
    {
        ArgumentNullException.ThrowIfNull(spirv);
        ArgumentNullException.ThrowIfNull(nameMap);

        if (spirv.Length < SpirvHeaderWordCount * 4 || (spirv.Length & 3) != 0)
        {
            throw new InvalidDataException("SPIR-V binary is malformed (size is not a multiple of 4 or shorter than its header).");
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(spirv);
        if (magic != SpirvMagicLE)
        {
            throw new InvalidDataException(
                $"Unsupported SPIR-V endianness or invalid magic: 0x{magic:X8} (expected 0x{SpirvMagicLE:X8}).");
        }

        if (nameMap.Names.Count == 0 && nameMap.MemberNames.Count == 0)
        {
            return (byte[])spirv.Clone();
        }

        var totalWords = spirv.Length / 4;
        var inputWords = new uint[totalWords];
        for (var i = 0; i < totalWords; i++)
        {
            inputWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(spirv.AsSpan(i * 4, 4));
        }

        var output = new List<uint>(totalWords + EstimateNameWordCount(nameMap));

        for (var i = 0; i < SpirvHeaderWordCount; i++)
        {
            output.Add(inputWords[i]);
        }

        var pos = SpirvHeaderWordCount;
        var injected = false;

        while (pos < inputWords.Length)
        {
            var word0 = inputWords[pos];
            var opcode = (ushort)(word0 & 0xFFFF);
            var wordCount = (ushort)(word0 >> 16);

            if (wordCount == 0)
            {
                throw new InvalidDataException($"SPIR-V instruction at word {pos} declared zero word count.");
            }

            var nextPos = pos + wordCount;
            if (nextPos > inputWords.Length)
            {
                throw new InvalidDataException($"SPIR-V instruction at word {pos} runs past end of module.");
            }

            if (opcode == OpName && wordCount >= 2 && nameMap.Names.ContainsKey(inputWords[pos + 1]))
            {
                pos = nextPos;
                continue;
            }

            if (opcode == OpMemberName && wordCount >= 3
                && nameMap.MemberNames.ContainsKey((inputWords[pos + 1], inputWords[pos + 2])))
            {
                pos = nextPos;
                continue;
            }

            if (!injected && !IsEarlySectionOpcode(opcode))
            {
                AppendNameInstructions(output, nameMap);
                injected = true;
            }

            for (var i = 0; i < wordCount; i++)
            {
                output.Add(inputWords[pos + i]);
            }

            pos = nextPos;
        }

        // Module with only header + section ≤ 7 — still append so the names land in a valid position.
        if (!injected)
        {
            AppendNameInstructions(output, nameMap);
        }

        var outputBytes = new byte[output.Count * 4];
        for (var i = 0; i < output.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(outputBytes.AsSpan(i * 4, 4), output[i]);
        }

        return outputBytes;
    }

    private static bool IsEarlySectionOpcode(ushort opcode) => opcode is
        OpCapability or
        OpExtension or
        OpExtInstImport or
        OpMemoryModel or
        OpEntryPoint or
        OpExecutionMode or
        OpExecutionModeId or
        OpString or
        OpSourceExtension or
        OpSource or
        OpSourceContinued or
        OpName or
        OpMemberName or
        OpModuleProcessed;

    private static void AppendNameInstructions(List<uint> output, SpirvNameMap nameMap)
    {
        foreach (var (id, name) in nameMap.Names)
        {
            var nameWords = EncodeLiteralString(name);
            var instructionWordCount = 2 + nameWords.Length;
            output.Add(PackOpcodeHeader(OpName, instructionWordCount));
            output.Add(id);
            output.AddRange(nameWords);
        }

        foreach (var ((typeId, memberIndex), name) in nameMap.MemberNames)
        {
            var nameWords = EncodeLiteralString(name);
            var instructionWordCount = 3 + nameWords.Length;
            output.Add(PackOpcodeHeader(OpMemberName, instructionWordCount));
            output.Add(typeId);
            output.Add(memberIndex);
            output.AddRange(nameWords);
        }
    }

    private static uint PackOpcodeHeader(ushort opcode, int wordCount)
        => opcode | ((uint)wordCount << 16);

    private static uint[] EncodeLiteralString(string text)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        // Append a null terminator and pad with zero bytes up to the next 4-byte boundary.
        var paddedBytes = (byteCount + 1 + 3) & ~3;
        var bytes = new byte[paddedBytes];
        Encoding.UTF8.GetBytes(text, bytes);

        var words = new uint[paddedBytes / 4];
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
        }

        return words;
    }

    private static int EstimateNameWordCount(SpirvNameMap nameMap)
    {
        var total = 0;
        foreach (var (_, name) in nameMap.Names)
        {
            total += 2 + (name.Length + 4) / 4;
        }
        foreach (var (_, name) in nameMap.MemberNames)
        {
            total += 3 + (name.Length + 4) / 4;
        }
        return total;
    }
}
