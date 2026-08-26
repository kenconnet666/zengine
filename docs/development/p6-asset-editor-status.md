# P6 Asset and Editor Status

Status: accepted on 2026-08-27 for the current Win11 x64 validation target.

## Implemented

- Stable strongly typed `AssetId<T>` derived from a logical name, separate from immutable SHA-256 content revisions.
- Versioned importer registry with reversible registration.
- Import context with explicit dependency tracking.
- Hash input includes importer id/version, source, dependency contents and cooked bytes.
- Content-addressed cache with atomic data/manifest writes.
- Typed asset handle generation swap only after a successful complete import.
- Content-aware polling monitor that detects source or dependency changes and leaves the current generation unchanged for identical output.
- Built-in SPIR-V, PNG and JSON scene importers with format validation.
- Importer registrations are owned by `PluginScope`, so an importer package unload removes all contributions in reverse order.
- Failed imports never replace the last complete handle generation.

## Editor Lab

- The editor is a normal ZEngine application using the same uppercase C# ZUI DSL, layout, system-font rasterization and Vulkan backend as the game UI.
- Initial typed panel registry includes scene hierarchy, inspector, asset browser, viewport, console, render graph, frame profiler, ECS archetypes, plugin graph, reload history and GPU resources.
- Panel registration is reversible, owner-labelled and rejects ambiguous ids with an actionable conflict error.
- `World.Inspect()` exposes immutable archetype/chunk snapshots without putting editor objects into the runtime world.
- Plugin graph inspection resolves the same SemVer dependency DAG used by activation.
- `PlayProcessSupervisor` launches the game with argument-safe `ProcessStartInfo.ArgumentList`, captures output and reports crashes while the editor process stays alive.

## Acceptance evidence

- Asset tests: 4 passed, including independent texture revision while shader and scene stay unchanged, importer-scope disposal, dependency-only rebuild and failure-safe generation retention.
- Editor tests: 3 passed, including all eleven panels rendered through ZUI, graph/world inspection, exact panel ownership and an intentional child exit code 23 with the editor registry still usable.
- Native Editor Lab: 11 panels, 57 UI nodes, 52 paint commands; Vulkan validation reported no errors.
- Captured frame: `artifacts/validation/editor-lab.png`, 1280x721, 200,348 bytes. Visual inspection confirmed readable Chinese system-font text, the three-column tool layout and Vulkan viewport content.

## Exit conditions

- Importer, shader, texture and scene revisions are independently rebuildable and swapped only after validation: met.
- A game crash does not close the editor: met by a real isolated process returning exit code 23.
- Plugin graph and reload inspector are available: met through built-in panels driven by typed snapshots.

## Deliberate boundaries

- `AssetMonitor` is the runtime-neutral content check. Filesystem watching, debounce and build orchestration remain a DevHost responsibility, so release runtimes do not start hidden watchers.
- The editor inspector consumes typed/generated metadata views; unrestricted reflection-based property mutation is intentionally absent.
- Current glyph drawing is correctness-first and emits raster runs as quads. A texture-atlas batch path remains a renderer optimization gate before public API freeze; it does not change the UI/component contract.
