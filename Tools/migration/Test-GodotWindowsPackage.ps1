[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [Parameter(Mandatory = $true)]
    [string]$SourceManifestPath,
    [string]$SourceCommit,
    [string]$GodotVersion,
    [string]$DotnetSdk,
    [string]$Configuration = 'Release',
    [string]$WorkflowRunId = '',
    [string]$WorkflowRef = ''
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Package root not found: $root" }
$exe = Join-Path $root 'Tactics.exe'
$pck = Join-Path $root 'Tactics.pck'
foreach ($required in @($exe, $pck, $SourceManifestPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf) -or (Get-Item $required).Length -eq 0) {
        throw "Required non-empty RC file is missing: $required"
    }
}

$stream = [IO.File]::OpenRead($exe)
try {
    $reader = [IO.BinaryReader]::new($stream)
    if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'Tactics.exe is not a PE executable (missing MZ header).' }
    $stream.Position = 0x3c
    $peOffset = $reader.ReadInt32()
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Tactics.exe is not a PE executable (missing PE signature).' }
    $machine = $reader.ReadUInt16()
    if ($machine -ne 0x8664) { throw ("Expected x86_64 PE machine 0x8664, found 0x{0:x4}." -f $machine) }
} finally { $stream.Dispose() }

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName)
if (-not ($files | Where-Object Extension -eq '.dll')) { throw 'RC package contains no managed assemblies.' }
$forbidden = @($files | Where-Object {
    $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' -or
    $_.Name -match '\.meta$' -or
    $_.FullName -match '[\\/](tests|addons[\\/]godot_ai|Assets|Packages|ProjectSettings)[\\/]' -or
    $_.Name -match '^(UnityEngine|UnityEditor).*\.dll$' -or
    $_.Name -match '(?i)(save|backup|quarantine).*\.json$'
})
if ($forbidden.Count -gt 0) {
    throw "Forbidden RC payload detected: $($forbidden.FullName -join ', ')"
}

$allowedRootFiles = @('Tactics.exe', 'Tactics.pck')
$unexpectedRootFiles = @(Get-ChildItem -LiteralPath $root -File | Where-Object {
    $_.Name -notin $allowedRootFiles -and $_.Extension -notin @('.dll', '.json')
})
if ($unexpectedRootFiles.Count -gt 0) {
    throw "Unexpected RC root files: $($unexpectedRootFiles.Name -join ', ')"
}
$unexpectedRootDirectories = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object {
    $_.Name -notmatch '^(data_|Tactics_Data$)'
})
if ($unexpectedRootDirectories.Count -gt 0) {
    throw "Unexpected RC root directories: $($unexpectedRootDirectories.Name -join ', ')"
}

$sourceManifestHash = (Get-FileHash -LiteralPath $SourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$payloadFiles = @($files | Where-Object { $_.Name -notin @('rc-manifest.json', 'rc-semantic-manifest.json', 'SHA256SUMS.txt') })
$entries = @($payloadFiles | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($root.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1).Replace('\', '/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$semantic = [ordered]@{
    schemaVersion = 1
    sourceCommit = $SourceCommit
    sourceBoundary = 'godot-owned-without-unity-v1'
    sourceManifestSha256 = $sourceManifestHash
    godotVersion = $GodotVersion
    dotnetSdk = $DotnetSdk
    configuration = $Configuration
    architecture = 'windows-x86_64'
    audio = 'deferred_no_payload'
    files = $entries
}
$semanticPath = Join-Path $root 'rc-semantic-manifest.json'
$semantic | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $semanticPath -Encoding utf8
$semanticHash = (Get-FileHash -LiteralPath $semanticPath -Algorithm SHA256).Hash.ToLowerInvariant()
$provenance = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    workflowRunId = $WorkflowRunId
    workflowRef = $WorkflowRef
    semanticManifestSha256 = $semanticHash
}
$provenance | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $root 'rc-manifest.json') -Encoding utf8

$sumFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName)
$sumLines = @($sumFiles | ForEach-Object {
    $relative = $_.FullName.Substring($root.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1).Replace('\', '/')
    "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
})
[IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'), $sumLines, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{ SemanticManifestSha256 = $semanticHash; FileCount = $sumFiles.Count }
