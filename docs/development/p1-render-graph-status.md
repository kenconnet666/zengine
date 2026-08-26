# P1 Render Graph Status

Status: P1A backend-neutral graph compiler accepted; Vulkan execution and advanced device paths remain in progress.

## Implemented in P1A

- Strongly typed `GpuHandle<T>` with slot generation validation.
- Render-thread-owned `GpuResourcePool<TTag, TResource>` with stale-handle rejection.
- Timeline-keyed `FrameRetirementQueue` for deferred destruction.
- Backend-neutral image, buffer, usage, format, sizing and allocator contracts.
- Strongly typed `RenderImage<TKind>` and `RenderBuffer<TKind>` graph handles.
- Single-scope pass DSL with `ref` pass data and cached typed execute delegates.
- Read, write and read-write resource declarations with explicit stage, access and layout semantics.
- Resource hazard dependency generation.
- Required `Before` and `After` pass constraints.
- Dead-pass culling from exported resources and side effects.
- Stable topological scheduling and dependency-cycle diagnostics.
- Synchronization barrier planning for image layouts and read/write hazards.
- Live-resource interval analysis.
- Conservative alias slots for compatible, non-overlapping transient resources.
- Parallel command-recording levels.
- Allocation-free execution of a compiled stable graph.
- Vulkan timeline-semaphore feature enablement and raw wrapper.
- GPU submission timeline signaling wired into the current renderer.
- Timeline-driven deferred-release collection on frame boundaries.
- Imported image initial-state declarations and external-to-first-pass barriers.
- Public compiled resource-access plan for backend execution.
- Vulkan stage, access and layout translation for Synchronization 2 barriers.
- Vulkan RenderGraph sink with typed backend command access.
- Actual `Frame.Clear -> Frame.Triangle -> Frame.Present` hardware graph.
- Separate dynamic-rendering scopes for clear and load/draw passes.

## Validation evidence

- `dotnet build ZEngine.slnx -c Debug`: zero warnings and zero errors.
- Microsoft Testing Platform: 17 passed, zero failed.
- RenderGraph suite: seven passed, including cycle, culling, barrier, alias and ownership diagnostics.
- Stable compiled execution: 10,000 executions measured zero managed bytes on the calling thread.
- Vulkan timeline semaphore: host signal, wait and counter-value test passed under Khronos validation.
- Render submission: two GPU frames advanced the timeline and released a deferred callback only after completion.
- Khronos validation: the graph-generated multi-pass barrier path rendered and presented with zero error-severity messages.
- Computer Use visual evidence: the graph-driven visible window displayed the expected interpolated RGB triangle after the renderer migration.
- `dotnet format ZEngine.slnx --verify-no-changes`: passed.

## Remaining P1

- Vulkan allocator baseline behind `IGpuAllocator`.
- Multi-frame command pools and frame contexts.
- Dynamic Rendering Local Read capability path.
- Descriptor Buffer primary path and fallback.
- Pipeline cache and failure-safe shader/pipeline hot reload.
- Debug labels and RenderDoc capture naming.
- Resize, minimize, restore and swapchain-generation rebuild.
- Hardware validation stress tests without a routine `vkDeviceWaitIdle` frame path.
