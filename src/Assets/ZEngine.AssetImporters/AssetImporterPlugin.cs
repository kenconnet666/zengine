using ZEngine.Assets;
using ZEngine.Plugins;

namespace ZEngine.AssetImporters;

public sealed class AssetImporterPlugin : EnginePlugin
{
    public override void Configure(PluginScope scope)
    {
        AssetImporterRegistry registry = scope.Require<AssetImporterRegistry>();
        scope.Own(registry.Register(new SpirvShaderImporter()));
        scope.Own(registry.Register(new PngTextureImporter()));
        scope.Own(registry.Register(new PpmTextureImporter()));
        scope.Own(registry.Register(new JsonSceneImporter()));
    }
}
