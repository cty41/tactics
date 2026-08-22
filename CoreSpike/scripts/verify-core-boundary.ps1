param(
    [string]$Root = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"
$resolvedRoot = (Resolve-Path $Root).Path
$sourceRoot = Join-Path $resolvedRoot "src/Tactics.Core"
$forbidden = 'UnityEngine|UnityEditor|Godot|TBSFramework|DG\.Tweening|Sirenix'

$violations = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter *.cs |
    Select-String -Pattern $forbidden -AllMatches

if ($violations) {
    $violations | ForEach-Object { $_.ToString() }
    throw "Tactics.Core contains forbidden engine or third-party references."
}

dotnet test (Join-Path $resolvedRoot "tests/Tactics.Core.Tests/Tactics.Core.Tests.csproj") --configuration Release --no-restore
