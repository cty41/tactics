[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = (Resolve-Path (Join-Path $repoRoot 'godot')).Path
$projectFile = Join-Path $projectRoot 'project.godot'
$adapterProject = Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj'

if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw "Godot executable not found: $GodotExecutable"
}
if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "Canonical Godot project file not found: $projectFile"
}
if (-not (Test-Path -LiteralPath $adapterProject -PathType Leaf)) {
    throw "Godot adapter project not found: $adapterProject"
}

function Invoke-Checked {
    param(
        [string]$Description,
        [scriptblock]$Command
    )

    Write-Host "== $Description =="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    Invoke-Checked 'Build Tactics.Godot.Adapter' {
        dotnet build $adapterProject --no-restore -c Debug
    }

    Invoke-Checked 'Poison Spear catalog and Core validation' {
        & $GodotExecutable --headless --path $projectRoot --validate-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Poison Spear Tween and Scope validation' {
        & $GodotExecutable --headless --path $projectRoot --play-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Godot editor/plugin initialization' {
        & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000
    }

    Write-Host "Godot migration verification passed. Canonical project: $projectRoot"
}
finally {
    Pop-Location
}
