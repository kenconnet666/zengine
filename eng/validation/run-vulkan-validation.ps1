[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipWindowSmoke
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$vulkanBin = Join-Path $repositoryRoot 'artifacts/tools/vulkan-sdk-portable/Bin'
$spirvValidator = Join-Path $vulkanBin 'spirv-val.exe'
$layerManifest = Join-Path $vulkanBin 'VkLayer_khronos_validation.json'

if (-not (Test-Path -LiteralPath $spirvValidator) -or
    -not (Test-Path -LiteralPath $layerManifest)) {
    throw 'Portable validation tools are missing. Run eng/tools/install-vulkan-validation.ps1 first.'
}

$previousLayerPath = $env:VK_LAYER_PATH
$previousLayers = $env:VK_INSTANCE_LAYERS

try {
    $env:VK_LAYER_PATH = $vulkanBin
    $env:VK_INSTANCE_LAYERS = 'VK_LAYER_KHRONOS_validation'

    & $spirvValidator (Join-Path $repositoryRoot 'shaders/compiled/triangle.vert.spv')
    if ($LASTEXITCODE -ne 0) {
        throw "Vertex SPIR-V validation failed with exit code $LASTEXITCODE."
    }

    & $spirvValidator (Join-Path $repositoryRoot 'shaders/compiled/triangle.frag.spv')
    if ($LASTEXITCODE -ne 0) {
        throw "Fragment SPIR-V validation failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipBuild) {
        dotnet build (Join-Path $repositoryRoot 'ZEngine.slnx') -c Debug --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE."
        }
    }

    dotnet test --solution (Join-Path $repositoryRoot 'ZEngine.slnx') -c Debug --no-build --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipWindowSmoke) {
        dotnet run `
            --project (Join-Path $repositoryRoot 'src/Host/ZEngine.Host/ZEngine.Host.csproj') `
            -c Debug `
            --no-build `
            -- `
            --resize-smoke
        if ($LASTEXITCODE -ne 0) {
            throw "Visible Vulkan smoke failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    $env:VK_LAYER_PATH = $previousLayerPath
    $env:VK_INSTANCE_LAYERS = $previousLayers
}
