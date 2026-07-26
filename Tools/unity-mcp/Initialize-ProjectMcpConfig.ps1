[CmdletBinding(DefaultParameterSetName = 'Initialize')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Initialize')]
    [Parameter(Mandatory, ParameterSetName = 'PrepareMigration')]
    [string]$Url,
    [Parameter(Mandatory, ParameterSetName = 'PrepareMigration')]
    [switch]$PrepareMigration,
    [Parameter(Mandatory, ParameterSetName = 'RestoreMigration')]
    [switch]$RestoreMigration
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sourcePath = Join-Path $projectRoot '.agents/mcp.json'
$backupPath = Join-Path $projectRoot '.agents/mcp.local.json'
$syncScript = Join-Path $PSScriptRoot 'Sync-ProjectMcpConfig.ps1'

function Get-ConfigDocument([string]$Endpoint) {
    return [ordered]@{
        mcpServers = [ordered]@{
            unityMCP = [ordered]@{
                url = $Endpoint
            }
        }
    }
}

function Validate-Endpoint([string]$Endpoint) {
    $uri = $null
    if (-not [Uri]::TryCreate($Endpoint, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'http' -or
        $uri.Host -ne '127.0.0.1' -or
        $uri.Port -lt 1 -or
        $uri.Port -gt 65535 -or
        $uri.AbsolutePath.TrimEnd('/') -ne '/mcp' -or
        $uri.Query -or
        $uri.Fragment) {
        throw "URL must be a loopback HTTP /mcp endpoint: $Endpoint"
    }
}

if ($RestoreMigration) {
    if (-not (Test-Path -LiteralPath $backupPath)) {
        throw "Migration backup is missing: $backupPath. Run with -PrepareMigration -Url <endpoint> before updating this worktree."
    }

    $Url = (Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json).mcpServers.unityMCP.url
}

Validate-Endpoint $Url
$document = Get-ConfigDocument $Url

if ($PrepareMigration) {
    $document | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $backupPath -NoNewline
    Write-Host "Saved worktree migration backup: $backupPath ($Url)"
    return
}

$document | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $sourcePath -NoNewline
& $syncScript
if (-not $?) {
    exit 1
}

Write-Host "Initialized Unity MCP worktree configuration: $Url"
