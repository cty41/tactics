[CmdletBinding()]
param(
    [ValidateSet('SeedZero', 'Batch')]
    [string]$Mode = 'Batch',

    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectRoot = Join-Path $repoRoot 'godot'
$testHost = Join-Path $projectRoot 'tests\Tactics.Godot.TestHost.csproj'
$settings = Join-Path $repoRoot 'Tactics.Godot.long-run.runsettings'
$filter = if ($Mode -eq 'SeedZero') {
    'FullyQualifiedName~DemonboundProductionSeedZeroWritesReplayableFailureEvidence'
} else {
    'FullyQualifiedName~DemonboundProductionThirtySeedRunsUseRealInputAndWriteReplayableMetrics'
}

Push-Location $repoRoot
try {
    if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
        throw "Godot executable was not found: $GodotExecutable"
    }
    $env:GODOT_BIN = $GodotExecutable
    dotnet restore $testHost --locked-mode -p:GodotProjectDir="$projectRoot\"
    if ($LASTEXITCODE -ne 0) { throw 'Godot long-run test host restore failed.' }
    dotnet build $testHost -c Debug --no-restore --no-incremental -m:1 -p:GodotProjectDir="$projectRoot\"
    if ($LASTEXITCODE -ne 0) { throw 'Godot long-run test host build failed.' }
    dotnet test $testHost -c Debug --no-restore --no-build --settings $settings `
        -p:GodotProjectDir="$projectRoot\" --filter $filter --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw "Demonbound long-run $Mode failed; inspect artifacts/gameplay-specs/godot for preserved evidence." }
}
finally {
    Pop-Location
}
