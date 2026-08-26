# P3 Plugin Reload Status

Status: P3A single-plugin R1 runtime and dependency graph accepted; multi-plugin closure transaction remains in progress.

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
- 1,000 immutable generation stages and real collectible ALC reloads passed in 70.114 seconds; all 1,001 unloaded-context WeakReferences were dead after diagnostic GC and retained managed heap stayed below 32 MiB.
- Generation directories are deletable in-process after unload because entry assemblies use `LoadFromStream`.

## Remaining P3

- Atomic multi-plugin staging and swap of a reverse dependency closure.
- Exact `plugin.lock.json` model and graph/package hash verification as one unit.
- Contract-generation R2 policy; schema changes remain R3 process restart.
