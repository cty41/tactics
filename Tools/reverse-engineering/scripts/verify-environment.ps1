[CmdletBinding()]
param(
    [string]$GhidraHome = 'D:\Program Files\ghidra_12.1.2_PUBLIC',
    [string]$ProjectRoot = 'D:\Ghi',
    [string]$ProjectName = 'mew',
    [string]$InputBinary = 'D:\SteamLibrary\steamapps\common\Mewgenics\Mewgenics.exe'
)

$ErrorActionPreference = 'Stop'
$toolRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $toolRoot 'manifests\mewgenics-analysis.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

$javaVersionText = (& java --version | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw 'Java is not available on PATH.'
}
$versionMatch = [regex]::Match($javaVersionText, '(?m)^(?:openjdk|java).*?(?<major>\d+)')
if (-not $versionMatch.Success) {
    throw "Could not parse Java version: $javaVersionText"
}
$javaMajor = [int]$versionMatch.Groups['major'].Value
if ($javaMajor -lt [int]$manifest.toolchain.minimum_java_major) {
    throw "Java $javaMajor is too old; Java $($manifest.toolchain.minimum_java_major)+ is required."
}

if (-not (Test-Path -LiteralPath $InputBinary)) {
    throw "Input binary does not exist: $InputBinary"
}
$inputItem = Get-Item -LiteralPath $InputBinary
$inputHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $InputBinary).Hash.ToLowerInvariant()
if ($inputItem.Length -ne [long]$manifest.subject.size_bytes -or $inputHash -ne $manifest.subject.sha256) {
    throw "Input binary does not match the manifest. Actual size=$($inputItem.Length), sha256=$inputHash"
}

$exportScript = Join-Path $PSScriptRoot 'ExportFunctionBundle.java'
$scriptHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $exportScript).Hash.ToLowerInvariant()
if ($scriptHash -ne $manifest.toolchain.export_script_sha256) {
    throw "Export script hash does not match the manifest. Actual sha256=$scriptHash"
}

& (Join-Path $PSScriptRoot 'export-mew.ps1') -GhidraHome $GhidraHome -ProjectRoot $ProjectRoot -ProjectName $ProjectName -ValidateOnly
if ($LASTEXITCODE -ne 0) {
    throw 'Export command validation failed.'
}

Write-Host "Environment verified: Ghidra $($manifest.toolchain.ghidra_version), Java $javaMajor, matching input and export script."
