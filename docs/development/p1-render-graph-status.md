# P1 Render Graph Status

Status: P1 Render Graph Lab accepted on the current Win11 x64 + RX 9070 GRE validation profile.

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
- Two independently owned frame slots, each with a command pool, command buffer and acquire semaphore.
- Timeline waits only when a frame slot or swapchain image generation is reused.
- Fence-free steady frame submission; `vkDeviceWaitIdle` remains disposal-only.
- `vkGetPhysicalDeviceFeatures2` capability chain for Vulkan 1.4 local read, buffer device address, Descriptor Buffer and Descriptor Heap.
- Runtime descriptor binding policy: Descriptor Heap first, Descriptor Buffer second, descriptor sets as portable fallback.
- RX 9070 GRE device creation with Dynamic Rendering Local Read and Descriptor Heap enabled.
- Hardware suites serialize same-process swapchain tests; this avoids unsafe driver stress from unrelated xUnit class concurrency while solution projects may still run concurrently.
- Pure C# Vulkan allocator behind `IGpuAllocator` with memory-type selection, aligned block suballocation, dedicated large allocations and free-range merging.
- Persistently mapped host-coherent upload/readback blocks and typed `VulkanBuffer` ownership.
- SPIR-V structural preflight and SHA-256 change detection.
- Transactional shader/pipeline replacement at frame boundaries.
- Timeline-retired old pipelines without `vkDeviceWaitIdle` on the reload path.
- Structured `Unchanged`, `Applied` and `Rejected` reload results.
- `VK_EXT_debug_utils` object names for image views, pipeline layouts and pipeline generations.
- Allocation-free per-pass command labels using pre-encoded compiled pass names.
- Shared `VkPipelineCache` across resize and shader generations with byte import/export.
- Descriptor Heap property query, device-address allocation, persistently mapped 1 MiB resource heap and per-frame `vkCmdBindResourceHeapEXT`.
- Async DXC development compiler, polling source monitor and atomic pipeline-cache file store.

## Validation evidence

- `dotnet build ZEngine.slnx -c Debug`: zero warnings and zero errors.
- Microsoft Testing Platform: 21 passed, zero failed.
- RenderGraph suite: seven passed, including cycle, culling, barrier, alias and ownership diagnostics.
- Stable compiled execution: 10,000 executions measured zero managed bytes on the calling thread.
- Vulkan timeline semaphore: host signal, wait and counter-value test passed under Khronos validation.
- Render submission: two GPU frames advanced the timeline and released a deferred callback only after completion.
- Khronos validation: the graph-generated multi-pass barrier path rendered and presented with zero error-severity messages.
- Computer Use visual evidence: the graph-driven visible window displayed the expected interpolated RGB triangle after the renderer migration.
- Multi-frame validation stress: clear and triangle tests each submitted eight frames across both frame slots with zero validation errors.
- Window-generation validation: resize, minimize, restore and two swapchain rebuilds completed with zero validation errors.
- Capability validation: RX 9070 GRE reported Descriptor Heap, Descriptor Buffer, Buffer Device Address and Dynamic Rendering Local Read; policy selected Descriptor Heap.
- Stability rerun: the complete 18-test validation suite passed three consecutive repetitions after GPU-test serialization.
- Allocator hardware test: two upload buffers shared one 16 MiB `VkDeviceMemory` block, mapped writes round-tripped, freed ranges merged and a device-local block stayed unmapped.
- Pipeline reload hardware test: a second real fragment module advanced shader generation 1 to 2; malformed SPIR-V was rejected and generation 2 rendered another frame.
- Automated host smoke applies the alternate pipeline, rejects an invalid candidate, then continues resize/minimize/restore rendering with zero validation errors.
- Debug-label gate: Win32 graph renderer resolved all debug-utils commands and submitted labeled passes without validation errors.
- Pipeline-cache gate: exported non-empty driver cache data and re-imported it into a second valid cache.
- Descriptor Heap gate: queried non-zero alignment/maximums, created a buffer with address allocation flags, obtained a non-zero GPU address and bound it on every validated frame.
- Development compiler gate: compiled valid HLSL through locked DXC, returned diagnostics for invalid HLSL, removed owned temp outputs and atomically replaced a cache file.
- Retirement stress: 1,000 callbacks stayed pending behind a submitted timeline and were all collected after the owning GPU generation completed.
- `dotnet format ZEngine.slnx --verify-no-changes`: passed.

## Deferred after P1

- Memory-budget telemetry, staging ring and non-coherent host-memory fallback.
- Actual resource-descriptor writes plus Descriptor Buffer/set command fallbacks.
- Dynamic Rendering Local Read is queried and enabled; a real multi-attachment input-read material is deferred until the first scene/UI consumer exists.
- Descriptor Heap is the validated primary path; Descriptor Buffer/set execution code is deferred until a fallback-profile machine is available.
