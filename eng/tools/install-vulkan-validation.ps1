[CmdletBinding()]
param(
    [string]$InstallerPath,
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$sdkVersion = '1.4.357.0'
$expectedSha256 = '81F474711E9042F4CD22B31B2F7A8870DB2E428B21586FB43DD80150BE97310D'
$downloadUri = "https://sdk.lunarg.com/sdk/download/$sdkVersion/windows/vulkansdk-windows-X64-$sdkVersion.exe"
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $repositoryRoot 'artifacts/tools/vulkan-sdk-portable'
}

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $downloadDirectory = Join-Path $repositoryRoot 'artifacts/downloads'
    New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null
    $InstallerPath = Join-Path $downloadDirectory "vulkansdk-windows-X64-$sdkVersion.exe"

    if (-not (Test-Path -LiteralPath $InstallerPath)) {
        Write-Host "Downloading Vulkan SDK $sdkVersion..."
        Invoke-WebRequest -Uri $downloadUri -OutFile $InstallerPath
    }
}

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedInstaller).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "Vulkan SDK archive SHA-256 mismatch. Expected $expectedSha256, got $actualSha256."
}

$tarCommand = Get-Command tar -ErrorAction Stop
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$portableFiles = @(
    'Bin/spirv-dis.exe',
    'Bin/SPIRV-Tools-shared.dll',
    'Bin/spirv-val.exe',
    'Bin/VkLayer_khronos_validation.dll',
    'Bin/VkLayer_khronos_validation.json'
)

& $tarCommand.Source -xf $resolvedInstaller -C $Destination @portableFiles
if ($LASTEXITCODE -ne 0) {
    throw "tar failed with exit code $LASTEXITCODE."
}

foreach ($portableFile in $portableFiles) {
    $expectedPath = Join-Path $Destination $portableFile
    if (-not (Test-Path -LiteralPath $expectedPath)) {
        throw "Expected Vulkan SDK file was not extracted: $expectedPath"
    }
}

Write-Host "Portable Vulkan validation tools are ready in $Destination"
