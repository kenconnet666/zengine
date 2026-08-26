# P3 Plugin Reload Status

Status: P3 Plugin Reload Lab accepted for trusted managed desktop plugins.

## Implemented

- Stable Plugin Abstractions assembly shared from Default ALC.
- Contract/Runtime fixture package split.
- SemVer 2 core/prerelease parsing and interval ranges.
- Required, optional and development dependency kinds.
- DAG validation, topological activation and cycle path diagnostics.
- Reverse required-consumer closure in consumer-first unload order.
- Immutable generation directory staging and optional SHA-256 verification.
- Collectible runtime ALC with `AssemblyDependencyResolver`.
- Explicit shared contract assembly identity.
- Native reload policy enforcement.
- Engine-owned typed HotState primitives.
- PluginScope-owned services, disposables and generation jobs.
- Staged configure/export validation.
- R1 safe-point suspend, hot-state restore, atomic swap and old-scope quiesce.
- Pre-swap rejection and post-swap probation rollback.
- WeakReference ALC collection diagnostics.
- Stream-based DLL/PDB shadow loading so immutable generation files are not mapped/locked.
- PluginSetRuntime closure staging with staged provider exports injected into dependent scopes.
- Atomic A/B/C registry swap while independent D keeps the same service instance.
- Whole-closure staging rejection and probation rollback.
- Deterministic `plugin.lock.json` model with exact versions, required edges and entry-DLL SHA-256.
- Asynchronous scoped retirement hook after plugin jobs drain and before service disposal.

## Validation targets

- A, B -> A, C -> B, D independent and E optional -> A graph.
- Compile/load failure keeps current generation active.
- Probation failure restores the old registry.
- Reload waits for plugin-owned work before disposal.
- 1,000 real collectible reloads leave no live old ALC.

## Validation evidence

- A, B -> A, C -> B, D independent and E optional -> A resolve with provider-first activation and `C, B, A` required unload closure.
- Missing required version and full dependency cycle path are rejected.
- Stateful reload preserves value 41 while disposing the old scoped service.
- Missing entry type rejects staging without changing the active generation.
- Probation rejection restores the exact previous service/registry.
- Reload remains pending while a plugin-owned blocking job runs, then completes after release and scope disposal.
- 1,000 immutable generation stages and real collectible ALC reloads passed twice in 70.114 and 73.001 seconds; all 1,001 unloaded-context WeakReferences were dead after diagnostic GC and retained managed heap stayed below 32 MiB, including the asynchronous retirement hook.
- Generation directories are deletable in-process after unload because entry assemblies use `LoadFromStream`.
- Real A/B/C/D fixture: reloading A builds A -> B -> C against staged services, swaps all three, disposes old C/B/A, and leaves D unchanged.
- Whole-closure probation and missing-entry staging failures keep the exact prior A/B/C service objects active.
- Lock round-trip validates the exact package set and rejects a tampered content hash.
- Plugin retirement hook completes before the old service is disposed, providing the integration point for Vulkan timeline retirement.

## Deferred after P3

- Contract-generation R2 policy; schema changes remain R3 process restart.
- Native plugin `Reloadable` remains disabled until a dedicated native callback/thread stress suite exists; default is runtime restart.
