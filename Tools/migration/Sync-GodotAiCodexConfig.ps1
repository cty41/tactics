[CmdletBinding()]
param(
    [switch]$ImportFromUser,
    [switch]$Check,
    [ValidateSet('phase3-observe', 'content-authoring', 'ui-input', 'presentation')]
    [string]$Profile = 'phase3-observe',
    [string]$ProjectRoot,
    [string]$UserConfig
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path $PSScriptRoot '..\..'
}
if ([string]::IsNullOrWhiteSpace($UserConfig)) {
    $UserConfig = Join-Path $env:USERPROFILE '.codex\config.toml'
}
if ($ImportFromUser -and $Check) {
    throw '-ImportFromUser and -Check are mutually exclusive.'
}

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$helper = Join-Path $PSScriptRoot 'godot_ai_codex_config.py'
$arguments = @(
    $helper,
    '--root', $resolvedRoot,
    '--user-config', $UserConfig
)
if (-not $Check -or $PSBoundParameters.ContainsKey('Profile')) {
    $arguments += @('--profile', $Profile)
}
if ($ImportFromUser) {
    $arguments += '--import-from-user'
}
elseif ($Check) {
    $arguments += '--check'
}

& python @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Godot AI Codex configuration failed with exit code $LASTEXITCODE."
}

if ($ImportFromUser) {
    Write-Output 'Restart Codex from D:\codes\tactics-worktrees\godot so the project-scoped MCP entry is discovered.'
}
