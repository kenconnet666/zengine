[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repositoryRoot 'samples/UiLab.Native/UiLab.Native.csproj'
$outputDirectory = Join-Path $repositoryRoot 'artifacts/validation/ui-scale-matrix'
$profiles = @(
    @{ Name = '100'; Scale = '1' },
    @{ Name = '125'; Scale = '1.25' },
    @{ Name = '150'; Scale = '1.5' },
    @{ Name = '200'; Scale = '2' }
)

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

if (-not $SkipBuild) {
    dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "UiLab.Native build failed with exit code $LASTEXITCODE."
    }
}

$vulkanBin = Join-Path $repositoryRoot 'artifacts/tools/vulkan-sdk-portable/Bin'
$layerManifest = Join-Path $vulkanBin 'VkLayer_khronos_validation.json'
$previousLayerPath = $env:VK_LAYER_PATH
$previousLayers = $env:VK_INSTANCE_LAYERS
$results = @()

try {
    if (Test-Path -LiteralPath $layerManifest) {
        $env:VK_LAYER_PATH = $vulkanBin
        $env:VK_INSTANCE_LAYERS = 'VK_LAYER_KHRONOS_validation'
    }

    foreach ($profile in $profiles) {
        $capture = Join-Path $outputDirectory "ui-scale-$($profile.Name).png"
        $runOutput = @(dotnet run `
            --project $project `
            -c $Configuration `
            --no-build `
            -- `
            --seconds=0.5 `
            --ui-scale=$($profile.Scale) `
            --capture=$capture)
        $runOutput | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            throw "UI scale $($profile.Scale) capture failed with exit code $LASTEXITCODE."
        }

        $file = Get-Item -LiteralPath $capture
        $telemetry = $runOutput |
            Where-Object { $_ -like 'Native UiLab:*' } |
            Select-Object -First 1
        $results += [pscustomobject]@{
            profile = $profile.Name
            scale = [double]$profile.Scale
            capture = $file.FullName
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $capture -Algorithm SHA256).Hash
            telemetry = $telemetry
        }
        Write-Host "UI scale $($profile.Scale): $($file.FullName) ($($file.Length) bytes)"
    }

    $manifest = Join-Path $outputDirectory 'ui-scale-matrix.json'
    $results |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $manifest -Encoding utf8NoBOM
    Write-Host "UI scale matrix manifest: $manifest"
}
finally {
    $env:VK_LAYER_PATH = $previousLayerPath
    $env:VK_INSTANCE_LAYERS = $previousLayers
}
