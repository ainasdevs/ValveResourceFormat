using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ValveKeyValue;
using ValveResourceFormat;
using ValveResourceFormat.CompiledShader;
using ValveResourceFormat.IO;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.Serialization.KeyValues;

namespace Tests;

public class AnimationGraphFilterTest
{
    [Test]
    public async Task FiltersGraphClipsBeforeLoadingAndDoesNotPopulateFullCache()
    {
        const string graphName = "graphs/test.vnmgraph";
        const string clipName = "animations/expensive.vnmclip";

        var (model, graphData) = CreateModelAndGraph(graphName, clipName);
        using var loader = new CountingLoader(graphName, graphData, clipName);

        var discovered = AnimationGraphLoader.GetClipNames(model, loader);
        await Assert.That(discovered).IsEquivalentTo([clipName]);

        var filtered = model.GetAllAnimations(loader, includeAnimationGraphClips: true, _ => false).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(filtered).IsEmpty();
            await Assert.That(loader.ClipLoadCount).IsZero();
        }

        var unfiltered = model.GetAllAnimations(loader).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(unfiltered).IsEmpty();
            await Assert.That(loader.ClipLoadCount).IsEqualTo(1);
        }
    }

    private static (TestModel Model, KVObject GraphData) CreateModelAndGraph(string graphName, string clipName)
    {
        var modelData = KVObject.Collection();
        var graphReferences = KVObject.Array();
        var graphReference = KVObject.Collection();
        graphReference["m_hGraph"] = graphName;
        graphReferences.Add(graphReference);
        modelData["m_animGraph2Refs"] = graphReferences;
        modelData["m_refAnimGroups"] = KVObject.Array();
        modelData["m_refAnimIncludeModels"] = KVObject.Array();

        var skeleton = KVObject.Collection();
        foreach (var key in new[] { "m_boneName", "m_nParent", "m_nFlag", "m_bonePosParent", "m_boneRotParent", "m_boneSphere" })
        {
            skeleton[key] = KVObject.Array();
        }

        modelData["m_modelSkeleton"] = skeleton;

        var modelInfo = KVObject.Collection();
        modelInfo["m_keyValueText"] = string.Empty;
        modelData["m_modelInfo"] = modelInfo;

        var graphData = KVObject.Collection();
        var resources = KVObject.Array();
        resources.Add(clipName);
        graphData["m_resources"] = resources;

        return (new TestModel(modelData) { Resource = new Resource() }, graphData);
    }

    private sealed class TestModel : Model
    {
        public TestModel(KVObject data)
        {
            Data = data;
        }
    }

    private sealed class CountingLoader : IFileLoader, IDisposable
    {
        private readonly string graphName;
        private readonly string clipName;
        private readonly Resource graphResource;

        private int clipLoadCount;

        public int ClipLoadCount => clipLoadCount;

        public CountingLoader(string graphName, KVObject graphData, string clipName)
        {
            this.graphName = graphName;
            this.clipName = clipName;
            graphResource = new Resource();
            graphResource.UnparsedBlocks.Add(new BinaryKV3(graphData, KV3IDLookup.Get("generic"), BlockType.DATA)
            {
                Resource = graphResource,
            });
        }

        public Resource? LoadFileCompiled(string file)
        {
            if (file == graphName)
            {
                return graphResource;
            }

            if (file == clipName)
            {
                Interlocked.Increment(ref clipLoadCount);
            }

            return null;
        }

        public Resource? LoadFile(string file) => null;

        public ShaderCollection? LoadShader(string shaderName) => null;

        public Stream? GetFileStream(string file) => null;

        public void Dispose() => graphResource.Dispose();
    }
}
