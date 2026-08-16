[CmdletBinding()]
param(
    [string]$ManifestPath = 'Tools/migration/manifest/retirement/unity-deletion-manifest-v1.json',
    [string]$DryRunReceiptPath = 'Tools/migration/manifest/retirement/unity-deletion-dry-run-v1.json',
    [string]$ResultPath = 'Tools/migration/manifest/retirement/unity-deletion-result-v1.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$manifestFile = [IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath))
$dryRunReceiptFile = [IO.Path]::GetFullPath((Join-Path $repoRoot $DryRunReceiptPath))
$resultFile = [IO.Path]::GetFullPath((Join-Path $repoRoot $ResultPath))
$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
$dryRunReceipt = Get-Content -LiteralPath $dryRunReceiptFile -Raw | ConvertFrom-Json
$manifestSha256 = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash.ToLowerInvariant()

if ($manifest.manifestId -ne 'unity-deletion-manifest-v1') { throw 'Unexpected Unity deletion manifest identity.' }
if ($manifestSha256 -ne [string]$dryRunReceipt.manifestSha256 -or $dryRunReceipt.result -ne 'passed') {
    throw 'The deletion manifest does not match a passing dry-run receipt.'
}
if ([int]$manifest.entryCount -ne @($manifest.entries).Count) { throw 'Deletion manifest entry count is inconsistent.' }
$tagObject = (git -C $repoRoot rev-parse unity-final-2026-08-08).Trim()
$archiveCommit = (git -C $repoRoot rev-list -n 1 unity-final-2026-08-08).Trim()
if ($tagObject -ne [string]$manifest.baseline.archiveTagObject -or
    $archiveCommit -ne [string]$manifest.baseline.archiveCommit) {
    throw 'The archived Unity tag identity drifted after review.'
}

$unityRootNames = @('Assets', 'Packages', 'ProjectSettings', 'UIElementsSchema')
$untrackedUnity = @(git -C $repoRoot ls-files --others --exclude-standard -- $unityRootNames)
if ($LASTEXITCODE -ne 0 -or $untrackedUnity.Count -gt 0) {
    throw "Unity roots contain untracked files: $($untrackedUnity -join ', ')"
}

$indexBlobs = @{}
foreach ($line in @(git -C $repoRoot ls-files -s)) {
    if ($line -match '^\d+\s+([0-9a-f]+)\s+\d+\t(.+)$') {
        $indexBlobs[$Matches[2]] = $Matches[1]
    }
}
$trackedStatus = @{}
foreach ($line in @(git -C $repoRoot status --porcelain=v1 --untracked-files=no)) {
    if ($line.Length -ge 4) { $trackedStatus[$line.Substring(3)] = $line.Substring(0, 2) }
}

$existingManifestTargets = @($manifest.entries | Where-Object {
    Test-Path -LiteralPath (Join-Path $repoRoot ([string]$_.path))
})
if ($existingManifestTargets.Count -ne 0 -and $existingManifestTargets.Count -ne [int]$manifest.entryCount) {
    throw "Deletion manifest is partially applied: $($existingManifestTargets.Count) of $($manifest.entryCount) targets remain."
}
$existingUnityRoots = @($unityRootNames | Where-Object {
    Test-Path -LiteralPath (Join-Path $repoRoot $_)
})
if ($existingManifestTargets.Count -eq 0 -and $existingUnityRoots.Count -eq 0) {
    if (-not (Test-Path -LiteralPath $resultFile)) {
        throw 'Unity retirement is already applied, but its result receipt is missing.'
    }
    $existingResult = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
    if ($existingResult.manifestSha256 -ne $manifestSha256 -or $existingResult.result -ne 'passed') {
        throw 'Unity retirement is already applied, but its result receipt is not a matching pass.'
    }
    Write-Output "UNITY_RETIREMENT_ALREADY_APPLIED files=$($manifest.entryCount) bytes=$($manifest.totalBytes)"
    return
}

foreach ($entry in $existingManifestTargets) {
    $relativePath = [string]$entry.path
    $target = [IO.Path]::GetFullPath((Join-Path $repoRoot $relativePath))
    if (-not $target.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest target escaped repository root: $relativePath"
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Manifest target is missing: $relativePath" }
    $indexBlob = [string]$indexBlobs[$relativePath]
    if ($indexBlob -ne [string]$entry.gitBlobSha1) { throw "Index blob drifted: $relativePath" }
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.worktreeSha256)) {
        $actualSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualSha256 -ne [string]$entry.worktreeSha256) { throw "Reviewed worktree bytes drifted: $relativePath" }
    }
    elseif ($trackedStatus.ContainsKey($relativePath)) {
        throw "Unexpected worktree modification in deletion target: $relativePath"
    }
}

$startedAt = [DateTimeOffset]::UtcNow
foreach ($entry in $existingManifestTargets) {
    Remove-Item -LiteralPath (Join-Path $repoRoot ([string]$entry.path)) -Force
}
foreach ($entry in $manifest.entries) {
    if (Test-Path -LiteralPath (Join-Path $repoRoot ([string]$entry.path))) {
        throw "Deleted manifest target survived: $($entry.path)"
    }
}
$ignoredUnityFiles = @(git -C $repoRoot ls-files --others -i --exclude-standard -- $unityRootNames)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inventory ignored Unity files.' }
$ignoredUnitySet = @{}
foreach ($ignoredPath in $ignoredUnityFiles) {
    $ignoredUnitySet[[string]$ignoredPath.Replace('\', '/')] = $true
}
$physicalUnityFiles = @()
foreach ($unityRootName in $unityRootNames) {
    $unityRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $unityRootName))
    if (Test-Path -LiteralPath $unityRoot -PathType Container) {
        $physicalUnityFiles += @(Get-ChildItem -LiteralPath $unityRoot -File -Recurse -Force)
    }
}
foreach ($physicalFile in $physicalUnityFiles) {
    $relativePath = $physicalFile.FullName.Substring($repoRoot.TrimEnd('\').Length + 1).Replace('\', '/')
    if (-not $ignoredUnitySet.ContainsKey($relativePath)) {
        throw "Unity root retained a file that is not proven ignored: $relativePath"
    }
}
if ($physicalUnityFiles.Count -ne $ignoredUnityFiles.Count) {
    throw "Ignored Unity inventory mismatch: physical=$($physicalUnityFiles.Count), git=$($ignoredUnityFiles.Count)."
}

foreach ($unityRootName in $unityRootNames) {
    $unityRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $unityRootName))
    if (Test-Path -LiteralPath $unityRoot -PathType Container) {
        Remove-Item -LiteralPath $unityRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $unityRoot) { throw "Unity root survived retirement: $unityRootName" }
}

$result = [ordered]@{
    schemaVersion = 1
    receiptId = 'unity-deletion-result-v1'
    manifestId = [string]$manifest.manifestId
    manifestSha256 = $manifestSha256
    archivedUnityTag = 'unity-final-2026-08-08'
    archiveTagObject = $tagObject
    archiveCommit = $archiveCommit
    deletedFileCount = [int]$manifest.entryCount
    deletedTrackedBytes = [long]$manifest.totalBytes
    deletedIgnoredFileCount = $ignoredUnityFiles.Count
    result = 'deleted_pending_post_verification'
    completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    elapsedSeconds = [Math]::Round(([DateTimeOffset]::UtcNow - $startedAt).TotalSeconds, 3)
}
$resultJson = ($result | ConvertTo-Json -Depth 4) + [Environment]::NewLine
[IO.File]::WriteAllText($resultFile, $resultJson, [Text.UTF8Encoding]::new($false))
Write-Output "UNITY_RETIREMENT_APPLIED files=$($manifest.entryCount) bytes=$($manifest.totalBytes)"
