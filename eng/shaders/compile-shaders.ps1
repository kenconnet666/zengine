[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$compiler = Join-Path $root "artifacts\tools\dxc\bin\x64\dxc.exe"
$source = Join-Path $root "shaders\triangle.hlsl"
$outputDirectory = Join-Path $root "shaders\compiled"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "DXC is missing. Run eng/tools/install-dxc.ps1 first."
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$vertexArguments = @(
    "-spirv",
    "-fspv-target-env=vulkan1.3",
    "-O3",
    "-T", "vs_6_0",
    "-E", "VSMain",
    "-Fo", (Join-Path $outputDirectory "triangle.vert.spv"),
    $source
)
& $compiler @vertexArguments
if ($LASTEXITCODE -ne 0) {
    throw "Vertex shader compilation failed."
}

$fragmentArguments = @(
    "-spirv",
    "-fspv-target-env=vulkan1.3",
    "-O3",
    "-T", "ps_6_0",
    "-E", "PSMain",
    "-Fo", (Join-Path $outputDirectory "triangle.frag.spv"),
    $source
)
& $compiler @fragmentArguments
if ($LASTEXITCODE -ne 0) {
    throw "Fragment shader compilation failed."
}

$shaderFiles = @(
    (Join-Path $outputDirectory "triangle.vert.spv"),
    (Join-Path $outputDirectory "triangle.frag.spv")
)
Get-FileHash -Algorithm SHA256 -LiteralPath $shaderFiles
