# Vulkan Registry

The checked-in vk.xml is the authoritative input for ZEngine.Vulkan.RegistryTool.

- Source: KhronosGroup/Vulkan-Docs
- Path: xml/vk.xml
- Commit: 7227da108bb407f1404edc5026ab4c0a9409c6a5
- SHA-256: 82BC15AEC2889B0058F01D019A0B34D77E3D502DA7B71B23F79882A489804957
- Update command: pwsh eng/registry/update-vulkan-registry.ps1

The update script pins a full Git commit and validates that the download contains a registry root before replacing the existing file.
