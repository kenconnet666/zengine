# P0 Native Core Status

Status: P0A accepted, P0B pending.

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
- dotnet test using Microsoft Testing Platform: six passed, zero failed.
- ZEngine.Host: detected Windows x64 and win-x64.
- Vulkan Raw probe: loaded vulkan-1.dll through NativeLibrary and reported Vulkan loader API 1.4.341.
- Vulkan Instance probe: enumerated AMD Radeon RX 9070 GRE as Vulkan 1.4.349, vendor 0x1002, device 0x7550.
- Vulkan Device probe: selected graphics queue family 0 and created a logical device and graphics queue.
- HelloZEngine: executed successfully.

## Remaining P0B

- Generated core enum, bitmask, structure and remaining command signatures.
- Win32 window and message pump.
- Debug messenger and validation layer.
- Extended physical-device feature/capability report.
- Window surface and swapchain.
- Validation-layer installation and zero-error triangle.
- Self-contained publish acceptance.
