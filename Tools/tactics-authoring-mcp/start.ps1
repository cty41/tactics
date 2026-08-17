$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectFile = Join-Path $PSScriptRoot 'Tactics.Authoring.Mcp.csproj'
$serverDll = Join-Path $PSScriptRoot 'bin\Debug\net9.0\Tactics.Authoring.Mcp.dll'
if (-not (Test-Path -LiteralPath $serverDll)) {
    $buildOutput = & dotnet build $projectFile --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($buildOutput -join [Environment]::NewLine) }
}
& dotnet $serverDll $projectRoot
exit $LASTEXITCODE
