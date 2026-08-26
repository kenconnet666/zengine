# P2 ECS and Jobs Status

Status: P2 ECS + Jobs Lab accepted on the current Win11 x64 profile.

## Implemented in P2A

- Generational `Entity` identity and stale-handle rejection.
- Stable component SchemaId plus dense 64-bit runtime component mask.
- Unmanaged-component registration with type and size metadata.
- Archetypes keyed by exact component mask.
- Fixed-capacity chunks with independent typed component columns (SoA).
- Spawn overloads for zero to three components.
- Add/remove structural migration with swap-back row maintenance.
- Deferred `EntityCommandBuffer` structural barrier.
- `Query<Write<T>, Read<U>>` access declaration.
- Allocation-free chunk enumeration with writable and read-only spans.
- Fixed-worker scheduler with local queues, cross-worker stealing and dependency counters.
- Generation-owned job groups with quiesce, cooperative cancellation and idle barrier.
- Job failure propagation that prevents dependent execution.
- ECS system access masks and non-conflicting parallel batches.
- Fixed-step accumulator with frame clamp, maximum catch-up and dropped-time diagnostics.
- Double-buffered typed RenderWorld SoA extraction.
- Minimal binary scene serializer keyed by stable SchemaId and unmanaged payload size.
- Incremental component generator pinned to Microsoft.CodeAnalysis.CSharp 5.9.0.
- Generated stable ID/name/size metadata plus build errors for non-partial, nested or managed-field components.

## P2A gates

- Entity index reuse advances generation and rejects the stale handle.
- Add/remove migration preserves every common component column.
- 10,000-entity query measured zero managed bytes across 100 stable updates.
- Release/x64 one-million-entity hardware benchmark on the current machine: 222.48 ms creation and 3.70 ms for one `Position += Velocity * dt` update over all entities.
- 4,000 jobs complete across four fixed workers; dependency order, cancellation and generation propagation are tested.
- Two non-conflicting systems rendezvous concurrently in one batch; a write/read conflict is split across an ordered barrier.
- RenderWorld remains detached from later Game World mutation and performs 100 warmed extractions at zero managed bytes.
- Scene round-trip restores two typed component columns without assembly-qualified type names.
- Generator driver produces metadata for an unmanaged partial component and reports `ZEC1002` for a managed field.

## Validation summary

- 36 tests pass across ECS, Jobs, generator, RenderGraph and Vulkan projects.
- Release/x64 benchmark remains the performance evidence; Debug timings printed by the normal gate are diagnostic only.
- ECS stable query and warmed RenderWorld extraction both measure zero managed bytes.
- The million-entity benchmark, conflict scheduler and RenderWorld separation satisfy the P2 exit conditions.

## Deferred after P2

- Generated serializers, migration code and query specializations beyond the two-access lab shape.
- Component schema changes remain an R3 runtime-process restart until P3 migration policy exists.
