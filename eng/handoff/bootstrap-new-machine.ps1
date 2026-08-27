[CmdletBinding()]
param(
    [switch]$InstallPortableTools,
    [switch]$Build,
    [switch]$Test,
    [switch]$FullValidation
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$repositoryRoot = (Resolve-Path -LiteralPath $repositoryRoot).Path
$solution = Join-Path $repositoryRoot 'ZEngine.slnx'
$globalJson = Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw |
    ConvertFrom-Json
$requiredSdk = $globalJson.sdk.version

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Command,
        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    $null = Get-Command git -ErrorAction Stop
    $null = Get-Command dotnet -ErrorAction Stop
    $actualSdk = (& dotnet --version).Trim()
    if ($actualSdk -ne $requiredSdk) {
        throw "ZEngine requires .NET SDK $requiredSdk, but dotnet resolved $actualSdk. Install the pinned preview SDK and retry."
    }

    Write-Host "Repository: $repositoryRoot"
    Write-Host "HEAD: $((& git rev-parse HEAD).Trim())"
    Write-Host "Git: $((& git --version).Trim())"
    Write-Host ".NET SDK: $actualSdk"
    Write-Host "OS: $([Runtime.InteropServices.RuntimeInformation]::OSDescription)"
    Write-Host "Architecture: $([Runtime.InteropServices.RuntimeInformation]::OSArchitecture)"
    if ($IsWindows) {
        Get-CimInstance Win32_VideoController |
            ForEach-Object { Write-Host "GPU: $($_.Name), driver $($_.DriverVersion)" }
    }

    Invoke-Checked -Description 'Git object verification' -Command {
        git fsck --full --no-dangling
    }
    Invoke-Checked -Description 'NuGet restore' -Command {
        dotnet restore $solution
    }

    if ($InstallPortableTools -or $FullValidation) {
        & (Join-Path $repositoryRoot 'eng/tools/install-dxc.ps1')
        & (Join-Path $repositoryRoot 'eng/tools/install-vulkan-validation.ps1')
    }

    if ($Build -or $Test -or $FullValidation) {
        Invoke-Checked -Description 'Release build' -Command {
            dotnet build $solution -c Release --no-restore
        }
    }

    if ($Test -or $FullValidation) {
        Invoke-Checked -Description 'Release test suite' -Command {
            dotnet test --solution $solution -c Release --no-build --no-restore --output Normal
        }
        Invoke-Checked -Description 'Formatting gate' -Command {
            dotnet format $solution --verify-no-changes --no-restore
        }
    }

    if ($FullValidation) {
        & (Join-Path $repositoryRoot 'eng/validation/run-vulkan-validation.ps1')
    }

    $status = @(& git status --short)
    if ($status.Count -eq 0) {
        Write-Host 'Bootstrap complete; the tracked worktree is clean.'
    }
    else {
        Write-Warning "Bootstrap completed, but tracked or untracked files remain:`n$($status -join [Environment]::NewLine)"
    }
}
finally {
    Pop-Location
}
