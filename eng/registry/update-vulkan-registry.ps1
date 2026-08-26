[CmdletBinding()]
param(
    [string] $Commit = "7227da108bb407f1404edc5026ab4c0a9409c6a5"
)

$ErrorActionPreference = "Stop"

$registryDirectory = $PSScriptRoot
$destination = Join-Path $registryDirectory "vk.xml"
$temporary = Join-Path $registryDirectory "vk.xml.download"
$uri = "https://raw.githubusercontent.com/KhronosGroup/Vulkan-Docs/$Commit/xml/vk.xml"

try {
    Invoke-WebRequest -Uri $uri -OutFile $temporary

    $document = [xml](Get-Content -LiteralPath $temporary -Raw)
    if ($document.registry -eq $null) {
        throw "Downloaded file is not a Vulkan registry."
    }

    Move-Item -LiteralPath $temporary -Destination $destination -Force
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }
}

$hash = Get-FileHash -LiteralPath $destination -Algorithm SHA256
Write-Output "Vulkan registry commit: $Commit"
Write-Output "Vulkan registry SHA256: $($hash.Hash)"
