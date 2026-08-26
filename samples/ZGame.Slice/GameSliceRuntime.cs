using ZEngine.AssetImporters;
using ZEngine.Assets;
using ZEngine.Plugins;
using ZEngine.Plugins.Runtime;
using ZGame.Slice.Contracts;

namespace ZGame.Slice;

public static class GameAssets
{
    public static AssetId<SceneAsset> FirstLevel { get; } =
        AssetId<SceneAsset>.FromName("game/scenes/first-level");

    public static AssetId<TextureAsset> Albedo { get; } =
        AssetId<TextureAsset>.FromName("game/textures/albedo");

    public static AssetId<ShaderAsset> TriangleShader { get; } =
        AssetId<ShaderAsset>.FromName("game/shaders/triangle-vertex");
}

public sealed class GameSliceRuntime : IAsyncDisposable
{
    private readonly string _contentRoot;
    private readonly PluginSetRuntime _plugins;
    private readonly IDisposable[] _importers;
    private readonly AssetLease<SceneAsset> _scene;
    private readonly AssetLease<TextureAsset> _texture;
    private readonly AssetLease<ShaderAsset> _shader;
    private IGameDirector _director;

    private GameSliceRuntime(
        string cacheRoot,
        string contentRoot,
        PluginSetRuntime plugins,
        IDisposable[] importers,
        AssetLease<SceneAsset> scene,
        AssetLease<TextureAsset> texture,
        AssetLease<ShaderAsset> shader,
        IGameDirector director,
        IReadOnlyList<AssetResidency> residency)
    {
        CacheRoot = cacheRoot;
        _contentRoot = contentRoot;
        _plugins = plugins;
        _importers = importers;
        _scene = scene;
        _texture = texture;
        _shader = shader;
        _director = director;
        AssetResidency = residency;
    }

    public GameFrame Frame => _director.Frame;

    public string CacheRoot { get; }

    public IReadOnlyList<AssetResidency> AssetResidency { get; }

    public IReadOnlyList<string> PluginIds { get; } =
        ["game.gameplay", "game.render", "game.director"];

    public GameFrame Tick(float deltaSeconds) => _director.Tick(deltaSeconds);

    public async Task<PluginSetReloadResult> ReloadGameplayAsync(
        CancellationToken cancellationToken = default)
    {
        PluginPackage gameplay = Packages(_contentRoot).Single(package =>
            package.Manifest.Id == "game.gameplay");
        PluginSetReloadResult result = await _plugins.ReloadClosureAsync(
            gameplay.Manifest.Id,
            gameplay,
            cancellationToken: cancellationToken);
        if (result.Status == PluginReloadStatus.Applied)
        {
            _director = _plugins.GetService<IGameDirector>();
        }

        return result;
    }

    public static async Task<GameSliceRuntime> CreateAsync(
        string? cacheRoot = null,
        string? contentRoot = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(cacheRoot ?? Path.Combine(
            Path.GetTempPath(),
            "zengine-game-slice-" + Environment.ProcessId));
        Directory.CreateDirectory(root);
        string content = Path.GetFullPath(contentRoot ?? Path.GetDirectoryName(
            typeof(GameSliceRuntime).Assembly.Location)!);
        AssetImporterRegistry registry = new();
        IDisposable[] importerRegistrations =
        [
            registry.Register(new JsonSceneImporter()),
            registry.Register(new PpmTextureImporter()),
            registry.Register(new SpirvShaderImporter())
        ];
        PluginSetRuntime? plugins = null;
        AssetLease<SceneAsset>? sceneLease = null;
        AssetLease<TextureAsset>? textureLease = null;
        AssetLease<ShaderAsset>? shaderLease = null;
        try
        {
            AssetPipeline pipeline = new(registry, new(Path.Combine(root, "assets")));
            AssetMonitor<SceneAsset> scene = new(
                pipeline,
                GameAssets.FirstLevel,
                Path.Combine(content, "AssetsSource", "level.scene.json"),
                "zengine.scene.json");
            AssetMonitor<TextureAsset> texture = new(
                pipeline,
                GameAssets.Albedo,
                Path.Combine(content, "AssetsSource", "albedo.ppm"),
                "zengine.texture.ppm");
            AssetMonitor<ShaderAsset> shader = new(
                pipeline,
                GameAssets.TriangleShader,
                Path.Combine(content, "Shaders", "triangle.vert.spv"),
                "zengine.shader.spirv");
            await scene.RefreshAsync(cancellationToken);
            await texture.RefreshAsync(cancellationToken);
            await shader.RefreshAsync(cancellationToken);

            AssetStreamCache<SceneAsset> sceneStream = new();
            AssetStreamCache<TextureAsset> textureStream = new();
            AssetStreamCache<ShaderAsset> shaderStream = new();
            sceneLease = await sceneStream.AcquireAsync(scene.Handle, cancellationToken);
            textureLease = await textureStream.AcquireAsync(texture.Handle, cancellationToken);
            shaderLease = await shaderStream.AcquireAsync(shader.Handle, cancellationToken);
            AssetResidency[] residency =
            [
                .. sceneStream.Snapshot(),
                .. textureStream.Snapshot(),
                .. shaderStream.Snapshot()
            ];

            plugins = new(Path.Combine(root, "plugin-generations"), workerCount: 2);
            await plugins.LoadInitialAsync(Packages(content), cancellationToken);
            return new(
                root,
                content,
                plugins,
                importerRegistrations,
                sceneLease,
                textureLease,
                shaderLease,
                plugins.GetService<IGameDirector>(),
                residency);
        }
        catch
        {
            sceneLease?.Dispose();
            textureLease?.Dispose();
            shaderLease?.Dispose();
            if (plugins is not null)
            {
                await plugins.DisposeAsync();
            }

            foreach (IDisposable registration in importerRegistrations.Reverse())
            {
                registration.Dispose();
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shader.Dispose();
        _texture.Dispose();
        _scene.Dispose();
        _director = null!;
        await _plugins.DisposeAsync();
        foreach (IDisposable registration in _importers.Reverse())
        {
            registration.Dispose();
        }
    }

    private static PluginPackage[] Packages(string contentRoot) =>
    [
        Package<IGameplaySimulation>(
            contentRoot,
            "game.gameplay",
            "gameplay",
            "ZGame.Slice.Gameplay.dll",
            "ZGame.Slice.Gameplay.GameplayPlugin"),
        Package<IGameRenderFeature>(
            contentRoot,
            "game.render",
            "render",
            "ZGame.Slice.RenderFeature.dll",
            "ZGame.Slice.RenderFeature.RenderFeaturePlugin",
            new PluginDependency("game.gameplay", "[1.0.0,2.0.0)")),
        Package<IGameDirector>(
            contentRoot,
            "game.director",
            "director",
            "ZGame.Slice.Director.dll",
            "ZGame.Slice.Director.DirectorPlugin",
            new PluginDependency("game.gameplay", "[1.0.0,2.0.0)"),
            new PluginDependency("game.render", "[1.0.0,2.0.0)"))
    ];

    private static PluginPackage Package<TService>(
        string contentRoot,
        string id,
        string directory,
        string assembly,
        string entryType,
        params PluginDependency[] dependencies)
        where TService : class =>
        new(
            new(
                id,
                "1.0.0",
                assembly,
                entryType,
                ["ZGame.Slice.Contracts"],
                dependencies,
                [typeof(TService).FullName!]),
            Path.Combine(contentRoot, "Plugins", directory));
}
