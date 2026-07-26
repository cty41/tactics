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
    $json = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $url = $json.mcpServers.unityMCP.url
    if ([string]::IsNullOrWhiteSpace($url)) {
        throw "Missing mcpServers.unityMCP.url in $ConfigPath"
    }

    $uri = $null
    if (-not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'http' -or
        $uri.Host -ne '127.0.0.1' -or
        $uri.Port -lt 1 -or
        $uri.Port -gt 65535 -or
        $uri.AbsolutePath.TrimEnd('/') -ne '/mcp' -or
        $uri.Query -or
        $uri.Fragment) {
        throw "unityMCP URL must be a loopback HTTP /mcp endpoint: $url"
    }

    return $uri.GetLeftPart([UriPartial]::Path).TrimEnd('/')
}

function Sync-TomlUrl([string]$TomlPath, [string]$ExpectedUrl, [bool]$CheckOnly) {
    $content = Get-Content -LiteralPath $TomlPath -Raw
    $pattern = '(?ms)(^\[mcp_servers\.unityMCP\]\s*\r?\n)(.*?)(?=^\[|\z)'
    $section = [regex]::Match($content, $pattern)
    if (-not $section.Success) {
        throw "Missing [mcp_servers.unityMCP] section in $TomlPath"
    }

    $urlMatch = [regex]::Match($section.Groups[2].Value, '(?m)^url\s*=\s*"([^"]+)"\s*$')
    if (-not $urlMatch.Success) {
        throw "Missing unityMCP url in $TomlPath"
    }

    $actualUrl = $urlMatch.Groups[1].Value
    if ($actualUrl -eq $ExpectedUrl) {
        return $false
    }

    if ($CheckOnly) {
        throw "Codex Unity MCP URL mismatch: expected $ExpectedUrl, found $actualUrl in $TomlPath"
    }

    $replacementSection = $section.Groups[1].Value + [regex]::Replace(
        $section.Groups[2].Value,
        '(?m)^url\s*=\s*"[^"]+"\s*$',
        ('url = "' + $ExpectedUrl + '"'),
        1)
    $updated = $content.Substring(0, $section.Index) + $replacementSection + $content.Substring($section.Index + $section.Length)
    Set-Content -LiteralPath $TomlPath -Value $updated -NoNewline
    return $true
}

function Sync-OpenCodeUrl([string]$ConfigPath, [string]$ExpectedUrl, [bool]$CheckOnly) {
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return $false
    }

    $json = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $actualUrl = $json.mcp.'unity-MCP'.url
    if ($actualUrl -eq $ExpectedUrl) {
        return $false
    }

    if ($CheckOnly) {
        throw "OpenCode Unity MCP URL mismatch: expected $ExpectedUrl, found $actualUrl in $ConfigPath"
    }

    $json.mcp.'unity-MCP'.url = $ExpectedUrl
    $json | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ConfigPath -NoNewline
    return $true
}

$projectRoot = Get-ProjectRoot
$sourcePath = Join-Path $projectRoot '.agents/mcp.json'
$codexPath = Join-Path $projectRoot '.codex/config.toml'
$openCodePath = Join-Path $projectRoot '.opencode/opencode.json'
$url = Get-UnityMcpUrl $sourcePath

$tomlChanged = Sync-TomlUrl $codexPath $url $Check
$openCodeChanged = Sync-OpenCodeUrl $openCodePath $url $Check

if ($Check) {
    Write-Host "Unity MCP configuration is synchronized: $url"
} elseif ($tomlChanged -or $openCodeChanged) {
    Write-Host "Synchronized Unity MCP configuration from ${sourcePath}: $url"
} else {
    Write-Host "Unity MCP configuration is already synchronized: $url"
}
