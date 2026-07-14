[CmdletBinding()]
param(
    [string]$GhidraHome = 'D:\Program Files\ghidra_12.1.2_PUBLIC',
    [string]$ProjectRoot = 'D:\Ghi',
    [string]$ProjectName = 'mew',
    [string]$ProgramName = 'Mewgenics.exe',
    [string]$RawOutputDir = 'D:\Ghi\export',
    [string]$LogDir = 'D:\Ghi\logs',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$toolRoot = Split-Path -Parent $PSScriptRoot
$targetFile = Join-Path $toolRoot 'targets\mewgenics-functions.json'
$exportScript = Join-Path $PSScriptRoot 'ExportFunctionBundle.java'
$headless = Join-Path $GhidraHome 'support\analyzeHeadless.bat'

foreach ($required in @($headless, $targetFile, $exportScript, (Join-Path $ProjectRoot "$ProjectName.gpr"))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required path does not exist: $required"
    }
}

$targetConfig = Get-Content -Raw -LiteralPath $targetFile | ConvertFrom-Json
$targets = @($targetConfig.targets)
if ($targets.Count -eq 0) {
    throw "No targets are defined in $targetFile"
}

$arguments = @(
    $ProjectRoot,
    $ProjectName,
    '-process',
    $ProgramName,
    '-readOnly',
    '-noanalysis',
    '-scriptPath',
    $PSScriptRoot,
    '-postScript',
    'ExportFunctionBundle.java',
    'mode=entry-bundle',
    "targets=$($targets -join ',')",
    'depth=1',
    "outDir=$RawOutputDir",
    '-scriptlog',
    (Join-Path $LogDir 'export-mew-script.log'),
    '-log',
    (Join-Path $LogDir 'export-mew-headless.log')
)

Write-Host "Ghidra: $headless"
Write-Host "Project: $(Join-Path $ProjectRoot $ProjectName)"
Write-Host "Program: $ProgramName"
Write-Host "Targets: $($targets.Count)"
Write-Host "Raw output: $RawOutputDir"

if ($ValidateOnly) {
    Write-Host 'Validation only; headless analysis was not started.'
    exit 0
}

New-Item -ItemType Directory -Force -Path $RawOutputDir, $LogDir | Out-Null
& $headless @arguments
if ($LASTEXITCODE -ne 0) {
    throw "analyzeHeadless failed with exit code $LASTEXITCODE"
}
