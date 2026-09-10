using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ValveKeyValue;
using ValveResourceFormat;
using ValveResourceFormat.CompiledShader;
using ValveResourceFormat.IO;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.ResourceTypes.ModelAnimation;
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

        var filtered = model.GetAllAnimations(loader, _ => false).ToList();

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

    [Test]
    public async Task SelectedClipCanDecodeAfterItsResourceIsDisposed()
    {
        const string graphName = "graphs/test.vnmgraph";
        const string clipName = "animations/idle_ak.vnmclip";
        var (model, graphData) = CreateModelAndGraph(graphName, clipName);
        var clipPath = Path.Combine(TestContext.TestDirectory!, "Files", "idle_ak.vnmclip_c");
        using var loader = new CountingLoader(graphName, graphData, clipName, clipPath);

        var animation = model.GetAllAnimations(loader, name => name == clipName).OfType<ClipAnimation>().Single();
        var frame = new FrameBone[animation.Clip.TrackCompressionSettings.Length];

        animation.Clip.ReadFrame(0, frame);

        using (Assert.Multiple())
        {
            await Assert.That(loader.ClipLoadCount).IsEqualTo(1);
            await Assert.That(animation.FrameCount).IsGreaterThan(0);
            await Assert.That(loader.LastClipResource!.Reader).IsNull();
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
        private readonly string? clipPath;

        public int ClipLoadCount { get; private set; }
        public Resource? LastClipResource { get; private set; }

        public CountingLoader(string graphName, KVObject graphData, string clipName, string? clipPath = null)
        {
            this.graphName = graphName;
            this.clipName = clipName;
            this.clipPath = clipPath;
            graphResource = new Resource();
            graphResource.Blocks.Add(new BinaryKV3(graphData, KV3IDLookup.Get("generic"), BlockType.DATA)
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
                ClipLoadCount++;
                if (clipPath != null)
                {
                    var resource = new Resource { FileName = clipPath };
                    resource.Read(clipPath);
                    LastClipResource = resource;
                    return resource;
                }
            }

            return null;
        }

        public Resource? LoadFile(string file) => null;

        public ShaderCollection? LoadShader(string shaderName) => null;

        public Stream? GetFileStream(string file) => null;

        public void Dispose() => graphResource.Dispose();
    }
}
