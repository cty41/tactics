[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe',
    [string]$ManifestPath = 'Tools/migration/manifest/retirement/unity-deletion-manifest-v1.json',
    [string]$ReceiptPath = 'Tools/migration/manifest/retirement/unity-deletion-dry-run-v1.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$manifestFile = [IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath))
$receiptFile = [IO.Path]::GetFullPath((Join-Path $repoRoot $ReceiptPath))
$systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$tempPrefix = $systemTemp + [IO.Path]::DirectorySeparatorChar
$testRoot = [IO.Path]::GetFullPath((Join-Path $systemTemp ("tactics-unity-retirement-" + [Guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Dry-run root escaped system temp: $testRoot"
}

$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
$manifestSha256 = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceCommit = (git -C $repoRoot rev-parse HEAD).Trim()
$started = [DateTimeOffset]::UtcNow
$verified = $false

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $tracked = @(git -C $repoRoot ls-files --cached)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0) { throw 'Unable to enumerate the staged repository snapshot.' }
    foreach ($relativePath in $tracked) {
        $source = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $destination = Join-Path $testRoot $relativePath
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent | Out-Null
        }
        Copy-Item -LiteralPath $source -Destination $destination
    }

    git -C $testRoot init --quiet
    git -C $testRoot config user.name 'Tactics Retirement Dry Run'
    git -C $testRoot config user.email 'retirement-dry-run@invalid.local'
    git -C $testRoot add --all
    if ($LASTEXITCODE -ne 0) { throw 'Unable to stage the dry-run source snapshot.' }
    $stagedBlobs = @{}
    foreach ($line in @(git -C $testRoot ls-files -s)) {
        if ($line -match '^\d+\s+([0-9a-f]+)\s+\d+\t(.+)$') {
            $stagedBlobs[$Matches[2]] = $Matches[1]
        }
    }

    foreach ($entry in $manifest.entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $testRoot ([string]$entry.path)))
        if (-not $target.StartsWith($testRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Manifest path escaped dry-run root: $($entry.path)"
        }
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            if (-not [string]::IsNullOrWhiteSpace([string]$entry.worktreeSha256)) {
                $actualSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($actualSha256 -ne [string]$entry.worktreeSha256) {
                    throw "Dirty manifest entry changed after review: $($entry.path)"
                }
            }
            else {
                $actualBlob = [string]$stagedBlobs[[string]$entry.path]
                if ([string]::IsNullOrWhiteSpace($actualBlob) -or $actualBlob -ne [string]$entry.gitBlobSha1) {
                    throw "Tracked manifest entry changed after review: $($entry.path)"
                }
            }
            Remove-Item -LiteralPath $target -Force
        }
    }

    foreach ($entry in $manifest.entries) {
        if (Test-Path -LiteralPath (Join-Path $testRoot ([string]$entry.path))) {
            throw "Manifest entry survived deletion: $($entry.path)"
        }
    }
    foreach ($forbidden in @('Assets', 'Packages', 'ProjectSettings', 'UIElementsSchema')) {
        $forbiddenRoot = Join-Path $testRoot $forbidden
        if (Test-Path -LiteralPath $forbiddenRoot -PathType Container) {
            $unexpectedFiles = @(Get-ChildItem -LiteralPath $forbiddenRoot -File -Recurse)
            if ($unexpectedFiles.Count -gt 0) {
                throw "Unity root retained files outside the manifest: $forbidden"
            }
            Remove-Item -LiteralPath $forbiddenRoot -Recurse -Force
        }
        if (Test-Path -LiteralPath $forbiddenRoot) {
            throw "Unity root survived deletion: $forbidden"
        }
    }
    foreach ($required in @(
        'Tactics.Godot.slnx',
        'Tactics.Godot.runsettings',
        'godot\project.godot',
        'Tools\godot\Verify-GodotProject.ps1',
        'src\Tactics.FrozenOracle.Tests\Tactics.FrozenOracle.Tests.csproj')) {
        if (-not (Test-Path -LiteralPath (Join-Path $testRoot $required))) {
            throw "Preserved contract is missing: $required"
        }
    }

    git -C $testRoot add --all
    git -C $testRoot commit --quiet -m "Unity retirement dry run $sourceCommit"
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize dry-run Git snapshot.' }

    & (Join-Path $testRoot 'Tools\godot\Verify-GodotProject.ps1') -GodotExecutable $GodotExecutable
    if ($LASTEXITCODE -ne 0) { throw "Godot mainline verifier failed with exit code $LASTEXITCODE." }
    $verified = $true
}
finally {
    if (Test-Path -LiteralPath $testRoot -PathType Container) {
        $resolved = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $testRoot).Path)
        if (-not $resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove dry-run root outside system temp: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

if (-not $verified) { throw 'Unity retirement dry run did not complete.' }
$receipt = [ordered]@{
    schemaVersion = 1
    receiptId = 'unity-deletion-dry-run-v1'
    sourceCommit = $sourceCommit
    manifestId = [string]$manifest.manifestId
    manifestSha256 = $manifestSha256
    entryCount = [int]$manifest.entryCount
    totalBytes = [long]$manifest.totalBytes
    verifier = 'Tools/godot/Verify-GodotProject.ps1'
    result = 'passed'
    productionWorkspaceDeleted = $false
    completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    elapsedSeconds = [Math]::Round(([DateTimeOffset]::UtcNow - $started).TotalSeconds, 3)
}
$receiptParent = Split-Path -Parent $receiptFile
if (-not (Test-Path -LiteralPath $receiptParent)) { New-Item -ItemType Directory -Path $receiptParent | Out-Null }
$receiptJson = ($receipt | ConvertTo-Json -Depth 4) + [Environment]::NewLine
[IO.File]::WriteAllText($receiptFile, $receiptJson, [Text.UTF8Encoding]::new($false))
Write-Output "UNITY_RETIREMENT_DRY_RUN_OK entries=$($manifest.entryCount) bytes=$($manifest.totalBytes)"
