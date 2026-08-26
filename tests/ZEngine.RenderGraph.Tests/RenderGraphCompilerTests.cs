using ZEngine.Graphics;
using Xunit;

namespace ZEngine.RenderGraph.Tests;

public sealed class RenderGraphCompilerTests
{
    [Fact]
    public void CompilesDependenciesCullsDeadPassAndSynthesizesBarriers()
    {
        RenderGraphBuilder graph = new();
        RenderImage<ColorTarget> hdr = graph.CreateImage<ColorTarget>(
            "Scene.Hdr",
            ImageDescriptor.Color2D(
                PixelFormat.Rgba16Float,
                ResourceSize2D.Swapchain));
        RenderImage<DepthTarget> depth = graph.CreateImage<DepthTarget>(
            "Scene.Depth",
            ImageDescriptor.Depth2D(
                PixelFormat.D32Float,
                ResourceSize2D.Swapchain));
        RenderImage<ColorTarget> post = graph.CreateImage<ColorTarget>(
            "Post.Color",
            ImageDescriptor.Color2D(
                PixelFormat.Rgba16Float,
                ResourceSize2D.Swapchain));
        RenderImage<ColorTarget> unused = graph.CreateImage<ColorTarget>(
            "Debug.Unused",
            ImageDescriptor.Color2D(
                PixelFormat.Rgba16Float,
                ResourceSize2D.Swapchain));

        graph.Pass<GBufferData>("Scene.GBuffer", pass =>
        {
            pass.Data.Color = pass.Write(
                hdr,
                ImageAccess.ColorAttachment(LoadOp.Clear));
            pass.Data.Depth = pass.Write(
                depth,
                ImageAccess.DepthAttachment(LoadOp.Clear));
            pass.Execute(static (ref RenderGraphCommandContext commands, in GBufferData _) =>
                commands.Emit("DrawGeometry"));
        });
        graph.Pass<LightingData>("Scene.Lighting", pass =>
        {
            pass.Data.Source = pass.Read(hdr);
            pass.Data.Destination = pass.Write(
                post,
                ImageAccess.ColorAttachment(LoadOp.Clear));
            pass.Execute(static (ref RenderGraphCommandContext commands, in LightingData _) =>
                commands.Emit("DrawLighting"));
        });
        graph.Pass<UnusedData>("Debug.Unused", pass =>
        {
            pass.Data.Target = pass.Write(
                unused,
                ImageAccess.ColorAttachment(LoadOp.Clear));
            pass.Execute(static (ref RenderGraphCommandContext _, in UnusedData _) => { });
        });
        graph.Pass<PresentData>("Present", pass =>
        {
            pass.Data.Source = pass.Read(post, ImageAccess.Present());
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext commands, in PresentData _) =>
                commands.Emit("Present"));
        });
        graph.Export(post);

        CompiledRenderGraph compiled = graph.Compile();

        Assert.Equal(
            ["Scene.GBuffer", "Scene.Lighting", "Present"],
            compiled.Passes.Select(pass => pass.Name));
        Assert.Equal(["Debug.Unused"], compiled.CulledPasses);
        Assert.Contains(
            compiled.Barriers,
            barrier =>
                barrier.Resource == "Scene.Hdr"
                && barrier.SourcePass == "Scene.GBuffer"
                && barrier.DestinationPass == "Scene.Lighting"
                && barrier.OldLayout == RenderImageLayout.ColorAttachment
                && barrier.NewLayout == RenderImageLayout.ShaderReadOnly);
        Assert.Contains(
            compiled.Barriers,
            barrier =>
                barrier.Resource == "Post.Color"
                && barrier.SourcePass == "Scene.Lighting"
                && barrier.DestinationPass == "Present"
                && barrier.NewLayout == RenderImageLayout.Present);
        Assert.DoesNotContain(
            compiled.ResourceLifetimes,
            lifetime => lifetime.Resource == "Debug.Unused");

        RecordingSink sink = new();
        compiled.Execute(sink);

        Assert.Equal(
            [
                "begin:Scene.GBuffer",
                "command:Scene.GBuffer:DrawGeometry",
                "end:Scene.GBuffer",
                "barrier:Scene.Hdr:Scene.GBuffer->Scene.Lighting",
                "begin:Scene.Lighting",
                "command:Scene.Lighting:DrawLighting",
                "end:Scene.Lighting",
                "barrier:Post.Color:Scene.Lighting->Present",
                "begin:Present",
                "command:Present:Present",
                "end:Present"
            ],
            sink.Events);
    }

    [Fact]
    public void FormsParallelRecordingLevelsFromExplicitDependencies()
    {
        RenderGraphBuilder graph = new();
        graph.Pass<EmptyData>("Independent.A", pass =>
        {
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });
        graph.Pass<EmptyData>("Independent.B", pass =>
        {
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });
        graph.Pass<EmptyData>("Join", pass =>
        {
            pass.After("Independent.A", "Independent.B").SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });

        CompiledRenderGraph compiled = graph.Compile();

        Assert.Equal(2, compiled.RecordingBatches.Count);
        Assert.Equal(
            ["Independent.A", "Independent.B"],
            compiled.RecordingBatches[0].Passes.Select(pass => pass.Name));
        Assert.Equal(["Join"], compiled.RecordingBatches[1].Passes.Select(pass => pass.Name));
    }

    [Fact]
    public void RejectsCyclesAndForeignHandles()
    {
        RenderGraphBuilder cyclic = new();
        cyclic.Pass<EmptyData>("A", pass =>
        {
            pass.After("B").SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });
        cyclic.Pass<EmptyData>("B", pass =>
        {
            pass.After("A").SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });

        RenderGraphCompileException cycle = Assert.Throws<RenderGraphCompileException>(
            cyclic.Compile);
        Assert.Contains("cycle", cycle.Message, StringComparison.OrdinalIgnoreCase);

        RenderGraphBuilder owner = new();
        RenderImage<ColorTarget> image = owner.CreateImage<ColorTarget>(
            "Owner.Image",
            ImageDescriptor.Color2D(
                PixelFormat.Rgba16Float,
                ResourceSize2D.Swapchain));
        RenderGraphBuilder foreign = new();
        Assert.Throws<InvalidOperationException>(() =>
            foreign.Pass<PresentData>("Foreign", pass =>
            {
                pass.Data.Source = pass.Read(image);
                pass.Execute(static (ref RenderGraphCommandContext _, in PresentData _) => { });
            }));
    }

    [Fact]
    public void AliasesCompatibleNonOverlappingTransientImages()
    {
        RenderGraphBuilder graph = new();
        ImageDescriptor descriptor = ImageDescriptor.Color2D(
            PixelFormat.Rgba16Float,
            ResourceSize2D.Swapchain);
        RenderImage<ColorTarget> first = graph.CreateImage<ColorTarget>("First", descriptor);
        RenderImage<ColorTarget> second = graph.CreateImage<ColorTarget>("Second", descriptor);

        graph.Pass<SingleImageData>("Write.First", pass =>
        {
            pass.Data.Image = pass.Write(first, ImageAccess.ColorAttachment());
            pass.Execute(static (ref RenderGraphCommandContext _, in SingleImageData _) => { });
        });
        graph.Pass<SingleImageData>("Consume.First", pass =>
        {
            pass.Data.Image = pass.Read(first);
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in SingleImageData _) => { });
        });
        graph.Pass<SingleImageData>("Write.Second", pass =>
        {
            pass.After("Consume.First");
            pass.Data.Image = pass.Write(second, ImageAccess.ColorAttachment());
            pass.Execute(static (ref RenderGraphCommandContext _, in SingleImageData _) => { });
        });
        graph.Pass<SingleImageData>("Consume.Second", pass =>
        {
            pass.Data.Image = pass.Read(second);
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in SingleImageData _) => { });
        });

        CompiledRenderGraph compiled = graph.Compile();
        ResourceLifetime firstLifetime = Assert.Single(
            compiled.ResourceLifetimes,
            lifetime => lifetime.Resource == "First");
        ResourceLifetime secondLifetime = Assert.Single(
            compiled.ResourceLifetimes,
            lifetime => lifetime.Resource == "Second");

        Assert.True(firstLifetime.LastUse < secondLifetime.FirstUse);
        Assert.Equal(firstLifetime.AliasSlot, secondLifetime.AliasSlot);
        Assert.True(firstLifetime.AliasSlot >= 0);
    }

    [Fact]
    public void StableExecutionAllocatesNoManagedMemory()
    {
        RenderGraphBuilder graph = new();
        graph.Pass<EmptyData>("Noop", pass =>
        {
            pass.SideEffect();
            pass.Execute(static (ref RenderGraphCommandContext _, in EmptyData _) => { });
        });
        CompiledRenderGraph compiled = graph.Compile();

        for (int index = 0; index < 32; index++)
        {
            compiled.Execute(NullRenderGraphCommandSink.Instance);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10_000; index++)
        {
            compiled.Execute(NullRenderGraphCommandSink.Instance);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private struct GBufferData
    {
        public RenderImage<ColorTarget> Color;
        public RenderImage<DepthTarget> Depth;
    }

    private struct LightingData
    {
        public RenderImage<ColorTarget> Source;
        public RenderImage<ColorTarget> Destination;
    }

    private struct PresentData
    {
        public RenderImage<ColorTarget> Source;
    }

    private struct UnusedData
    {
        public RenderImage<ColorTarget> Target;
    }

    private struct SingleImageData
    {
        public RenderImage<ColorTarget> Image;
    }

    private readonly struct EmptyData;

    private sealed class RecordingSink : IRenderGraphCommandSink
    {
        public List<string> Events { get; } = [];

        public void ApplyBarrier(in RenderBarrier barrier) =>
            Events.Add(
                $"barrier:{barrier.Resource}:{barrier.SourcePass}->{barrier.DestinationPass}");

        public void BeginPass(CompiledRenderPass pass) =>
            Events.Add($"begin:{pass.Name}");

        public void Command(string passName, string command) =>
            Events.Add($"command:{passName}:{command}");

        public void EndPass(CompiledRenderPass pass) =>
            Events.Add($"end:{pass.Name}");
    }
}
