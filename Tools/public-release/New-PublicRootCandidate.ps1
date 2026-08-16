[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$DestinationRoot,
    [string]$BranchName = 'main',
    [string]$CommitMessage = 'feat: publish the Godot Tactics source root'
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceRoot).Path
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (Test-Path -LiteralPath $destination) {
    throw "Public candidate destination already exists: $destination"
}

Push-Location $source
try {
    python Tools/public-release/validate_public_candidate.py --root $source --candidate
    if ($LASTEXITCODE -ne 0) { throw 'Source tree failed the public candidate policy.' }

    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0) {
        throw 'Unable to enumerate the public candidate source tree.'
    }
    $dirty = @(git status --porcelain)
    if ($dirty.Count -ne 0) {
        throw 'Public candidate source must be committed and clean before history reconstruction.'
    }

    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($relative in $tracked) {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $source $relative))
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Tracked public file is missing: $relative"
        }
        $targetPath = Join-Path $destination $relative
        $targetDirectory = Split-Path -Parent $targetPath
        if (-not (Test-Path -LiteralPath $targetDirectory)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath
    }

    git -C $destination init --initial-branch=$BranchName
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize the public candidate repository.' }
    git -C $destination lfs install --local
    git -C $destination add --all
    git -C $destination -c user.name=cty41 -c user.email=opensource@users.noreply.github.com `
        commit -m $CommitMessage
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create the public root commit.' }

    $commitCount = (git -C $destination rev-list --count HEAD).Trim()
    $parentLine = (git -C $destination rev-list --parents -n 1 HEAD).Trim().Split(' ')
    if ($commitCount -ne '1' -or $parentLine.Count -ne 1) {
        throw "Public candidate history is not a single root commit: count=$commitCount parents=$($parentLine.Count - 1)"
    }
    python (Join-Path $destination 'Tools/public-release/validate_public_candidate.py') `
        --root $destination --candidate
    if ($LASTEXITCODE -ne 0) { throw 'Reconstructed public root failed policy validation.' }
    Write-Host "Public root candidate created: $destination"
    Write-Host "Root commit: $(git -C $destination rev-parse HEAD)"
}
finally {
    Pop-Location
}
