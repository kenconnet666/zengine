# P0 Native Core Status

Status: P0 functional slice accepted; validation-layer follow-up pending.

## Implemented in P0A

- Exact .NET 11 Preview 7 SDK lock.
- Microsoft Testing Platform runner selection.
- Central build and package configuration.
- ZEngine.slnx project graph.
- Seed executable moved to samples/HelloZEngine.
- Core generation identifier and engine version.
- Platform environment detection.
- Vulkan registry model, XML reader and C# code writer.
- Registry command-line generator.
- Vulkan Raw system-loader wrapper using unmanaged function pointers.
- Host environment probe.
- Vulkan 1.4 instance creation.
- Physical-device enumeration and property report.
- Graphics queue-family selection.
- Logical-device and graphics-queue creation.
- Win32 window class, hidden/visible window creation and message pump.
- VkSurfaceKHR creation and presentation-support query.
- Surface format, present mode and capability query.
- VK_KHR_swapchain logical-device enablement.
- Swapchain creation and image enumeration.
- Swapchain image-view creation.
- Command pool/buffer and one-frame synchronization.
- Synchronization2 image layout transitions.
- Vulkan 1.4 dynamic-rendering clear pass.
- QueueSubmit2 and QueuePresentKHR frame loop.
- Locked DXC 1.9.2607 tool bootstrap.
- HLSL vertex/fragment shader and reproducible SPIR-V output.
- Shader-module and pipeline-layout creation.
- Vulkan 1.4 dynamic-rendering graphics pipeline.
- Dynamic viewport/scissor and vkCmdDraw triangle.
- Unit and hardware smoke tests.

## Pinned Vulkan registry

- Upstream: KhronosGroup/Vulkan-Docs
- Commit: 7227da108bb407f1404edc5026ab4c0a9409c6a5
- Specification update: Vulkan 1.4.359
- vk.xml SHA-256: 82BC15AEC2889B0058F01D019A0B34D77E3D502DA7B71B23F79882A489804957
- Generated handles: 63
- Parsed commands: 865

## Validation evidence

- dotnet build ZEngine.slnx -c Debug: passed with zero warnings and zero errors.
- dotnet test using Microsoft Testing Platform: nine passed, zero failed.
- ZEngine.Host: detected Windows x64 and win-x64.
- Vulkan Raw probe: loaded vulkan-1.dll through NativeLibrary and reported Vulkan loader API 1.4.341.
- Vulkan Instance probe: enumerated AMD Radeon RX 9070 GRE as Vulkan 1.4.349, vendor 0x1002, device 0x7550.
- Vulkan Device probe: selected graphics queue family 0 and created a logical device and graphics queue.
- Hidden-window test: created Win32 VkSurfaceKHR and a swapchain with at least two images.
- Visible-window smoke: created a 944x501 B8G8R8A8Srgb FIFO swapchain with three images and pumped events for two seconds.
- Self-contained win-x64 Release publish: produced and executed artifacts/publish/win-x64/ZEngine.Host.exe.
- Hidden render test: cleared and presented two swapchain frames through dynamic rendering.
- Hidden triangle test: created shader modules and graphics pipeline, drew and presented two frames.
- Computer Use visual evidence: captured the visible ZEngine Vulkan 1.4 Smoke window with a correctly interpolated RGB triangle.
- Release self-contained triangle smoke: loaded packaged SPIR-V and presented the same pipeline.
- HelloZEngine: executed successfully.

## Remaining P0B

- Generated core enum, bitmask, structure and remaining command signatures.
- Debug messenger and validation layer.
- Extended physical-device feature/capability report.
- Validation-layer installation and zero-error rendered frame.
