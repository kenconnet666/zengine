# Tool bootstraps

The repository does not require machine-wide SDK installation for its pinned
shader and validation checks.

## DXC

`install-dxc.ps1` downloads and verifies the pinned DXC distribution used by
`eng/shaders/compile-shaders.ps1`.

- Release: 1.9.2607
- Asset: `dxc_2026_07_29.zip`
- Archive SHA-256: `A1DFB116BA3EEAE6A1582291B53A8E7BF65AD760676BD3194685C8F7367CD241`

## Vulkan validation

`install-vulkan-validation.ps1` downloads Vulkan SDK 1.4.357.0, verifies its
SHA-256, and extracts only the Khronos validation layer and SPIR-V validation
tools under ignored `artifacts/tools/vulkan-sdk-portable`.

Run the complete local acceptance gate with:

```powershell
pwsh -NoProfile -File eng/tools/install-vulkan-validation.ps1
pwsh -NoProfile -File eng/validation/run-vulkan-validation.ps1
```

The validation runner restores the caller's Vulkan layer environment variables
after it finishes.
