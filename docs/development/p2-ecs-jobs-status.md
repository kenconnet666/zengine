# P2 ECS and Jobs Status

Status: P2A archetype/chunk data plane in progress.

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

## P2A gates

- Entity index reuse advances generation and rejects the stale handle.
- Add/remove migration preserves every common component column.
- 10,000-entity query measured zero managed bytes across 100 stable updates.
- Release/x64 one-million-entity hardware benchmark on the current machine: 222.48 ms creation and 3.70 ms for one `Position += Velocity * dt` update over all entities.

## Remaining P2

- Fixed worker job scheduler, work stealing, dependencies and cancellation.
- Access-conflict system DAG and parallel batches.
- Fixed/variable engine loop.
- RenderWorld extraction snapshot.
- Minimal schema-based scene serializer.
- Source-generated component/query metadata and diagnostics.
