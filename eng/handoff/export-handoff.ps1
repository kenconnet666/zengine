[CmdletBinding()]
param(
    [string]$Destination,
    [switch]$IncludeEvidence,
    [switch]$IncludePublishedGame
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$repositoryRoot = (Resolve-Path -LiteralPath $repositoryRoot).Path

Push-Location $repositoryRoot
try {
    $status = @(& git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the Git worktree.'
    }

    if ($status.Count -ne 0) {
        throw "The Git worktree must be clean before export:`n$($status -join [Environment]::NewLine)"
    }

    $branch = (& git branch --show-current).Trim()
    $head = (& git rev-parse HEAD).Trim()
    $shortHead = (& git rev-parse --short=12 HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($branch) -or
        [string]::IsNullOrWhiteSpace($head)) {
        throw 'The handoff must be exported from a named branch with a valid HEAD.'
    }

    if ([string]::IsNullOrWhiteSpace($Destination)) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $Destination = Join-Path $repositoryRoot "artifacts/handoff/zengine-$stamp-$shortHead"
    }
    elseif (-not [IO.Path]::IsPathRooted($Destination)) {
        $Destination = Join-Path $repositoryRoot $Destination
    }

    $destinationPath = [IO.Path]::GetFullPath($Destination)
    if (Test-Path -LiteralPath $destinationPath) {
        if (@(Get-ChildItem -LiteralPath $destinationPath -Force).Count -ne 0) {
            throw "Handoff destination already exists and is not empty: $destinationPath"
        }
    }
    else {
        New-Item -ItemType Directory -Path $destinationPath | Out-Null
    }

    $bundleName = "zengine-$shortHead.bundle"
    $bundlePath = Join-Path $destinationPath $bundleName
    & git bundle create $bundlePath $branch --tags
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $bundlePath)) {
        throw "Git bundle creation failed with exit code $LASTEXITCODE."
    }

    $bundleVerification = @(& git bundle verify $bundlePath 2>&1) |
        ForEach-Object { $_.ToString() }
    if ($LASTEXITCODE -ne 0) {
        throw "Git bundle verification failed:`n$($bundleVerification -join [Environment]::NewLine)"
    }

    $remoteUrl = (& git remote get-url origin 2>$null | Select-Object -First 1)
    $tracking = (& git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>$null)
    $ahead = $null
    $behind = $null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($tracking)) {
        $counts = ((& git rev-list --left-right --count "$tracking...HEAD").Trim() -split '\s+')
        if ($LASTEXITCODE -eq 0 -and $counts.Count -eq 2) {
            $behind = [int]$counts[0]
            $ahead = [int]$counts[1]
        }
    }

    $evidence = @()
    if ($IncludeEvidence) {
        $evidenceDirectory = Join-Path $destinationPath 'evidence'
        New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null
        $evidenceNames = @(
            'native-ui-capture.png',
            'editor-lab.png',
            'game-slice.png',
            'published-game-slice.png'
        )
        foreach ($name in $evidenceNames) {
            $source = Join-Path $repositoryRoot "artifacts/validation/$name"
            if (Test-Path -LiteralPath $source) {
                $target = Join-Path $evidenceDirectory $name
                Copy-Item -LiteralPath $source -Destination $target
                $evidence += [IO.Path]::GetRelativePath($destinationPath, $target)
            }
        }
    }

    $publishedGameArchive = $null
    if ($IncludePublishedGame) {
        $publishedGame = Join-Path $repositoryRoot 'artifacts/publish/game-slice'
        if (-not (Test-Path -LiteralPath $publishedGame)) {
            throw 'Published game output is missing. Publish it before using -IncludePublishedGame.'
        }

        $publishedGameArchive = Join-Path $destinationPath 'zgame-slice-win-x64.zip'
        Compress-Archive -Path (Join-Path $publishedGame '*') -DestinationPath $publishedGameArchive
        $publishedGameArchive = [IO.Path]::GetRelativePath(
            $destinationPath,
            $publishedGameArchive)
    }

    $gpu = $null
    if ($IsWindows) {
        $gpu = @(Get-CimInstance Win32_VideoController | ForEach-Object {
            [ordered]@{
                name = $_.Name
                driverVersion = $_.DriverVersion
            }
        })
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        repository = 'zengine'
        repositoryRootAtExport = $repositoryRoot
        branch = $branch
        head = $head
        shortHead = $shortHead
        commitCount = [int](& git rev-list --count HEAD)
        remoteUrl = $remoteUrl
        trackingBranch = $tracking
        commitsAheadOfTracking = $ahead
        commitsBehindTracking = $behind
        bundle = $bundleName
        bundleVerification = $bundleVerification
        dotnetSdk = (& dotnet --version).Trim()
        powerShell = $PSVersionTable.PSVersion.ToString()
        os = [Environment]::OSVersion.VersionString
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        gpu = $gpu
        evidence = $evidence
        publishedGameArchive = $publishedGameArchive
        authoritativeDocuments = @(
            'README.md',
            'docs/handoff/README.md',
            'docs/architecture/zengine-unified-csharp-ui-blazor-vulkan-blueprint.md',
            'docs/development/p7-game-slice-status.md'
        )
    }

    $manifestPath = Join-Path $destinationPath 'handoff-manifest.json'
    $manifestJson = $manifest | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText(
        $manifestPath,
        $manifestJson + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    $checksumPath = Join-Path $destinationPath 'SHA256SUMS.txt'
    $payloadFiles = Get-ChildItem -LiteralPath $destinationPath -File -Recurse |
        Where-Object FullName -ne $checksumPath |
        Sort-Object FullName
    $checksumLines = foreach ($file in $payloadFiles) {
        $relative = [IO.Path]::GetRelativePath($destinationPath, $file.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "$hash *$relative"
    }
    [IO.File]::WriteAllLines(
        $checksumPath,
        $checksumLines,
        [Text.UTF8Encoding]::new($false))

    Write-Host "Handoff export ready: $destinationPath"
    Write-Host "Branch: $branch"
    Write-Host "HEAD: $head"
    Write-Host "Bundle: $bundlePath"
    Write-Host "Checksums: $checksumPath"
}
finally {
    Pop-Location
}
