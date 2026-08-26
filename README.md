# ZEngine

ZEngine is an experimental pure-C# engine and UI stack targeting .NET 11 Preview 7. The validated desktop path is Win11 x64 with Vulkan 1.4 on a Radeon RX 9070 GRE; portable contracts isolate future Linux, Android and Apple adapters without pretending those platforms are already tested.

The P0-P7 architecture labs are implemented: generated Vulkan bindings, Win32 surface/swapchain, RenderGraph and timeline retirement, chunked ECS/jobs, collectible dependent plugins, shared Blazor/Vulkan ZUI, Agent/MCP feedback, content-addressed assets, the Vulkan editor lab and a published real game slice.

## Run the real slice

```powershell
dotnet run --project samples/ZGame.Slice/ZGame.Slice.csproj -c Release -- --seconds=3
```

Headless performance and dependent-plugin reload:

```powershell
dotnet run --project samples/ZGame.Slice/ZGame.Slice.csproj -c Release -- --headless --frames=1000 --reload
```

Full validation with the portable Khronos validation layer:

```powershell
./eng/validation/run-vulkan-validation.ps1
```

Self-contained Win-x64 publish:

```powershell
dotnet publish samples/ZGame.Slice/ZGame.Slice.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish/game-slice
```

## Documents

- Architecture and accepted decisions: `docs/architecture/zengine-unified-csharp-ui-blazor-vulkan-blueprint.md`
- Per-stage implementation evidence: `docs/development/p0-native-core-status.md` through `docs/development/p7-game-slice-status.md`
- Rejected browser-first predecessor: `docs/archive/rejected/2026-08-26-zui-webgpu-fullstack-blueprint-rejected.md`

This repository is still before v0.1 API freeze. Platform folders beyond Windows are architectural allocation points, not claims of validation.
