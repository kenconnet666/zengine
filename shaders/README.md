# Shaders

Triangle shaders are authored in HLSL and compiled to SPIR-V by eng/shaders/compile-shaders.ps1.

- Vertex SPIR-V SHA-256: F925240BE130987059DDBE15F48274131DA90C098BB8AB72EFAF198EB1EDA3C6
- Fragment SPIR-V SHA-256: 48AAA1E6BBE195125983686477F21586CBA6C01F8054B3ED45705BC11773C83E
- Target environment: Vulkan 1.3 SPIR-V environment, consumed by the Vulkan 1.4 runtime path.

The compiled files are committed so a clean checkout can build and run without downloading DXC.
