[CmdletBinding()]
param(
    [switch]$Check,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments
)

$ErrorActionPreference = 'Stop'

if ($RemainingArguments -contains '--check') {
    $Check = $true
}

if ($RemainingArguments.Count -gt 0 -and ($RemainingArguments | Where-Object { $_ -ne '--check' })) {
    throw "Unsupported argument(s): $($RemainingArguments -join ', ')"
}

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}

function Get-UnityMcpUrl([string]$ConfigPath) {
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        throw "Missing worktree-local MCP configuration: $ConfigPath. Run Initialize-ProjectMcpConfig.ps1 -Url <http://127.0.0.1:PORT/mcp>."
    }

    $json = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $url = $json.mcpServers.unityMCP.url
    $uri = $null
    if ([string]::IsNullOrWhiteSpace($url) -or
        -not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'http' -or
        $uri.Host -ne '127.0.0.1' -or
        $uri.Port -lt 1 -or
        $uri.Port -gt 65535 -or
        $uri.AbsolutePath.TrimEnd('/') -ne '/mcp' -or
        $uri.Query -or
        $uri.Fragment) {
        throw "unityMCP URL must be a loopback HTTP /mcp endpoint: $url"
    }

    return $uri.GetLeftPart([UriPartial]::Authority).TrimEnd('/') + '/mcp'
}

function Get-RenderedTemplate([string]$TemplatePath, [string]$Url) {
    $template = Get-Content -LiteralPath $TemplatePath -Raw
    if ($template -notmatch '__UNITY_MCP_URL__') {
        throw "Template does not contain __UNITY_MCP_URL__: $TemplatePath"
    }

    return $template.Replace('__UNITY_MCP_URL__', $Url)
}

function Test-RenderedFile([string]$Path, [string]$Expected, [bool]$CheckOnly) {
    $actual = if (Test-Path -LiteralPath $Path) { Get-Content -LiteralPath $Path -Raw } else { $null }
    $matches = $actual -eq $Expected
    if ($matches) {
        return $false
    }

    if ($CheckOnly) {
        throw "Generated MCP configuration is stale or missing: $Path. Run Sync-ProjectMcpConfig.ps1 without --check."
    }

    Set-Content -LiteralPath $Path -Value $Expected -NoNewline
    return $true
}

$projectRoot = Get-ProjectRoot
$sourcePath = Join-Path $projectRoot '.agents/mcp.json'
$url = Get-UnityMcpUrl $sourcePath
$targets = @(
    @{ Template = (Join-Path $projectRoot '.codex/config.template.toml'); Output = (Join-Path $projectRoot '.codex/config.toml') },
    @{ Template = (Join-Path $projectRoot '.opencode/opencode.template.json'); Output = (Join-Path $projectRoot '.opencode/opencode.json') }
)

$changed = $false
foreach ($target in $targets) {
    $rendered = Get-RenderedTemplate $target.Template $url
    if (Test-RenderedFile $target.Output $rendered $Check) {
        $changed = $true
    }
}

if ($Check) {
    Write-Host "Unity MCP worktree configuration is synchronized: $url"
} elseif ($changed) {
    Write-Host "Rendered Unity MCP worktree configuration from ${sourcePath}: $url"
} else {
    Write-Host "Unity MCP worktree configuration is already synchronized: $url"
}
