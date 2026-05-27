$ErrorActionPreference = "Stop"

$mode = if ($args.Count -gt 0) { $args[0] } else { "" }
$repoRoot = "D:\codes\tactics"
$hookScript = Join-Path $repoRoot ".codex\hooks\unity_auto_compile_guard.py"

$currentPath = (Get-Location).Path
if (-not $currentPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    exit 0
}

if (-not (Test-Path -LiteralPath $hookScript)) {
    exit 0
}

& py -3 $hookScript $mode
exit $LASTEXITCODE
