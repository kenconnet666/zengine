[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$version = "1.9.2607"
$assetDate = "2026_07_29"
$uri = "https://github.com/microsoft/DirectXShaderCompiler/releases/download/v$version/dxc_$assetDate.zip"
$artifacts = Join-Path $PSScriptRoot "..\..\artifacts"
$downloadDirectory = Join-Path $artifacts "downloads"
$toolDirectory = Join-Path $artifacts "tools\dxc"
$archive = Join-Path $downloadDirectory "dxc_$assetDate.zip"

New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri $uri -OutFile $archive
}

$hash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
if ($hash.Hash -ne "A1DFB116BA3EEAE6A1582291B53A8E7BF65AD760676BD3194685C8F7367CD241") {
    throw "DXC archive hash mismatch: $($hash.Hash)"
}

Expand-Archive -LiteralPath $archive -DestinationPath $toolDirectory -Force

$compiler = Join-Path $toolDirectory "bin\x64\dxc.exe"
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "DXC executable was not found after extraction."
}

& $compiler --version
