[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$verificationRoot = Join-Path $systemTemp ("tactics-godot-owned-" + [Guid]::NewGuid().ToString('N'))
$verificationRoot = [System.IO.Path]::GetFullPath($verificationRoot)
$requiredPrefix = $systemTemp + [System.IO.Path]::DirectorySeparatorChar
if (-not $verificationRoot.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Verification root escaped the system temp directory: $verificationRoot"
}

try {
    New-Item -ItemType Directory -Path $verificationRoot | Out-Null
    $trackedFiles = @(git -C $repoRoot ls-files)
    if ($LASTEXITCODE -ne 0 -or $trackedFiles.Count -eq 0) { throw 'Unable to enumerate tracked repository files.' }
    $workingFiles = @(
        'Tools/migration/Test-GodotOwnedWithoutUnity.ps1',
        'Tools/godot/Verify-GodotProject.ps1',
        'godot/tests/GdUnit4TestRunnerScene.cs.txt',
        'godot/tests/Tactics.Godot.TestHost.csproj'
    )
    $trackedFiles = @($trackedFiles + $workingFiles | Select-Object -Unique)
    foreach ($relativePath in $trackedFiles) {
        if ($relativePath -match '^(Assets|Packages|ProjectSettings|UIElementsSchema)/' -or
            $relativePath -match '^src/Tactics\.UnityOracle\.Tests/') { continue }
        $source = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $destination = Join-Path $verificationRoot $relativePath
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent | Out-Null
        }
        Copy-Item -LiteralPath $source -Destination $destination
    }

    foreach ($forbidden in @('Assets', 'Packages', 'ProjectSettings', 'UIElementsSchema', 'src\Tactics.UnityOracle.Tests')) {
        if (Test-Path -LiteralPath (Join-Path $verificationRoot $forbidden)) {
            throw "Unity path leaked into Godot-owned verification copy: $forbidden"
        }
    }

    # Project-scoped agent tooling is intentionally outside the product repository.
    # Remove its copied registration so the ownership proof starts without optional MCP files.
    $isolatedProject = Join-Path $verificationRoot 'godot\project.godot'
    $projectText = [System.IO.File]::ReadAllText($isolatedProject)
    $projectText = [Regex]::Replace($projectText,
        '(?m)^_mcp_game_helper="\*res://addons/godot_ai/runtime/game_helper\.gd"\r?\n', '')
    $projectText = $projectText.Replace(
        'enabled=PackedStringArray("res://addons/godot_ai/plugin.cfg", "res://addons/tactics_tooling/plugin.cfg")',
        'enabled=PackedStringArray("res://addons/tactics_tooling/plugin.cfg")')
    [System.IO.File]::WriteAllText($isolatedProject, $projectText,
        [System.Text.UTF8Encoding]::new($false))

    & (Join-Path $verificationRoot 'Tools\godot\Verify-GodotProject.ps1') `
        -GodotExecutable $GodotExecutable
    if ($LASTEXITCODE -ne 0) { throw "Godot-owned verification failed with exit code $LASTEXITCODE." }
}
finally {
    if (Test-Path -LiteralPath $verificationRoot -PathType Container) {
        $resolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $verificationRoot).Path)
        if (-not $resolved.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove verification root outside system temp: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
