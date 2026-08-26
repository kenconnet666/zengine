# P7 First Real Game Slice Status

Status: accepted on 2026-08-27 for the current Win11 x64 + Radeon RX 9070 GRE validation target.

## Integrated slice

- `game.gameplay` is a collectible plugin that owns a chunked ECS world and updates 50,000 moving entities through typed `Query<Write<T>, Read<U>>` access.
- `game.render` is a required dependent plugin that owns a compiled five-pass RenderGraph (`Clear → World → Effects → Hud → Present`).
- `game.director` depends on both services and coordinates simulation and presentation only through `ZGame.Slice.Contracts` interfaces.
- Reloading gameplay atomically rebuilds the exact required closure `game.gameplay, game.render, game.director`.
- Plugin entry assemblies and all managed dependencies load from streams. The real slice exposed and fixed the previous Windows generation-file lock for non-shared dependencies.
- `GameHud` contains a reactive HUD and menu in the same uppercase, strongly typed C# ZUI DSL used by the labs and editor.
- Scene JSON, ASCII PPM texture and SPIR-V shader are independently imported into immutable content revisions and acquired through reference-counted typed stream caches.
- Runtime code references generated/static `GameAssets` ids rather than source-path strings.

## Automated acceptance

- P7 integration tests: 3 passed.
  - Three real ALC plugins, three resident assets and five render passes work together.
  - Gameplay reload applies the full dependent closure.
  - HUD state and menu events produce a local UI diff while the stable button NodeId survives.
  - 2,000 measured frames with 50,000 entities remain below the 2.5 ms CPU budget and at or below 1 KiB managed allocation after warmup.
- Asset suite: 5 passed, including shared resident revisions and retirement after the last lease.
- Full Release solution acceptance: 67 passed, zero failed, zero skipped across 13 test projects in 1 minute 15.968 seconds.
- Full Debug validation-layer acceptance: 67 passed, zero failed and zero skipped; SPIR-V validation, swapchain resize/minimize/restore, shader accept/reject reload, Native UiLab and the native game slice all completed with zero Vulkan validation errors.
- Full Release build: zero warnings and zero errors.
- Full formatting gate: `dotnet format ZEngine.slnx --verify-no-changes --no-restore` passed.

## Measured runtime evidence

- Development headless, 1,000 frames after gameplay closure reload: 0.1210 ms/frame and 728 bytes total managed allocation.
- Self-contained published headless executable, 1,000 frames after gameplay closure reload: 0.1174 ms/frame and 728 bytes total managed allocation.
- Native development run, three seconds: 277 frames / 92.2 FPS, 0.171 ms average gameplay+graph CPU time, three resident assets, zero Vulkan validation errors.
- Native self-contained run, two seconds: 167 frames / 83.3 FPS, 0.157 ms average gameplay+graph CPU time, three resident assets, zero Vulkan validation errors.
- Native Debug run with the Khronos validation layer forced: 96 frames / 47.6 FPS, 1.199 ms average gameplay+graph CPU time, three resident assets, zero validation errors.
- Native capture: `artifacts/validation/game-slice.png`, 1120x661, 238,192 bytes. Visual inspection confirmed the Chinese HUD/menu, live metrics and Vulkan triangle.
- Published capture: `artifacts/validation/published-game-slice.png`, 238,324 bytes.

## Publish evidence

- Command: `dotnet publish samples/ZGame.Slice/ZGame.Slice.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish/game-slice`.
- Output: 283 files, 87,846,904 bytes (about 83.8 MiB).
- The published executable was launched directly, not through `dotnet`, and exercised assets, dependent ALC reload, ECS, RenderGraph, HUD and Vulkan.
- The .NET runtime is self-contained; a conformant system Vulkan loader/driver remains an intentional platform prerequisite.

## Exit conditions

- Gameplay plugin: met.
- Render feature plugin: met.
- Dependent plugin: met with an actual three-node required closure and reload.
- HUD/menu: met on the shared UI runtime and native Vulkan backend.
- Asset streaming: met with typed, deduplicated, reference-counted revision leases.
- Publish and performance: met for Win11 x64 on the current machine.

## API-freeze decision

P0 through P7 now provide enough implementation evidence to begin a separate v0.1 API review. They do not automatically freeze the public surface. The remaining known optimization before a performance-oriented UI freeze is replacing correctness-first per-glyph raster-run quads with a batched GPU glyph-atlas path.

The final all-up Debug run also exposed a dependency-registration race in the job scheduler: a completed node could publish its completion flag before its failure. Completion error and completion state are now published under the same lock, with a 2,000-iteration regression test.
