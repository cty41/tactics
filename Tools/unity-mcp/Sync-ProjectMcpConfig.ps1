[CmdletBinding()]
param(
    [switch]$Check,
    [ValidateSet('Sync', 'Initialize', 'PrepareMigration', 'RestoreMigration')]
    [string]$Operation = 'Sync',
    [string]$InitializeUrl,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:TestInternalOperationFailureInjected = $false

if ($RemainingArguments -contains '--check') {
    $Check = $true
}
if ($null -ne $RemainingArguments -and
    $RemainingArguments.Length -gt 0 -and
    ($RemainingArguments | Where-Object { $_ -ne '--check' })) {
    throw "Unsupported argument(s): $($RemainingArguments -join ', ')"
}
if ($Check -and $Operation -ne 'Sync') {
    throw '--check is only valid for the Sync operation.'
}
if ($Operation -in @('Initialize', 'PrepareMigration') -and
    [string]::IsNullOrWhiteSpace($InitializeUrl)) {
    throw "$Operation requires InitializeUrl."
}
if ($Operation -notin @('Initialize', 'PrepareMigration') -and
    -not [string]::IsNullOrWhiteSpace($InitializeUrl)) {
    throw "InitializeUrl is not valid for $Operation."
}

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}

function Throw-StrictJsonError([string]$Context) {
    throw "Invalid strict JSON in $Context."
}

function Skip-StrictJsonWhitespace {
    while ($script:StrictJsonIndex -lt $script:StrictJsonText.Length) {
        $character = $script:StrictJsonText[$script:StrictJsonIndex]
        if ($character -ne ' ' -and $character -ne "`t" -and
            $character -ne "`r" -and $character -ne "`n") {
            break
        }
        $script:StrictJsonIndex++
    }
}

function Read-Utf8TextStrict([string]$Path, [string]$Context) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xef -and
            $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf) {
            throw 'UTF-8 BOM is not allowed.'
        }
        $encoding = New-Object System.Text.UTF8Encoding($false, $true)
        return $encoding.GetString($bytes)
    }
    catch {
        throw "Invalid UTF-8 in ${Context}: $($_.Exception.Message)"
    }
}

function Read-StrictJsonUnicodeEscape {
    if ($script:StrictJsonIndex + 4 -gt $script:StrictJsonText.Length) {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $hex = $script:StrictJsonText.Substring($script:StrictJsonIndex, 4)
    if ($hex -notmatch '^[0-9A-Fa-f]{4}$') {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $script:StrictJsonIndex += 4
    return [Convert]::ToInt32($hex, 16)
}

function Read-StrictJsonString {
    if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length -or
        $script:StrictJsonText[$script:StrictJsonIndex] -ne '"') {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $script:StrictJsonIndex++
    $builder = New-Object System.Text.StringBuilder
    while ($script:StrictJsonIndex -lt $script:StrictJsonText.Length) {
        $character = $script:StrictJsonText[$script:StrictJsonIndex]
        $script:StrictJsonIndex++
        if ($character -eq '"') {
            return $builder.ToString()
        }
        if ([int]$character -lt 0x20) {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        if ($character -ne '\') {
            [void]$builder.Append($character)
            continue
        }
        if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length) {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        $escape = $script:StrictJsonText[$script:StrictJsonIndex]
        $script:StrictJsonIndex++
        switch ($escape) {
            '"' { [void]$builder.Append('"') }
            '\' { [void]$builder.Append('\') }
            '/' { [void]$builder.Append('/') }
            'b' { [void]$builder.Append([char]0x08) }
            'f' { [void]$builder.Append([char]0x0c) }
            'n' { [void]$builder.Append([char]0x0a) }
            'r' { [void]$builder.Append([char]0x0d) }
            't' { [void]$builder.Append([char]0x09) }
            'u' {
                $codePoint = Read-StrictJsonUnicodeEscape
                if ($codePoint -ge 0xd800 -and $codePoint -le 0xdbff) {
                    if ($script:StrictJsonIndex + 2 -gt $script:StrictJsonText.Length -or
                        $script:StrictJsonText[$script:StrictJsonIndex] -ne '\' -or
                        $script:StrictJsonText[$script:StrictJsonIndex + 1] -ne 'u') {
                        Throw-StrictJsonError $script:StrictJsonContext
                    }
                    $script:StrictJsonIndex += 2
                    $lowCodePoint = Read-StrictJsonUnicodeEscape
                    if ($lowCodePoint -lt 0xdc00 -or $lowCodePoint -gt 0xdfff) {
                        Throw-StrictJsonError $script:StrictJsonContext
                    }
                    [void]$builder.Append([char]$codePoint)
                    [void]$builder.Append([char]$lowCodePoint)
                }
                elseif ($codePoint -ge 0xdc00 -and $codePoint -le 0xdfff) {
                    Throw-StrictJsonError $script:StrictJsonContext
                }
                else {
                    [void]$builder.Append([char]$codePoint)
                }
            }
            default { Throw-StrictJsonError $script:StrictJsonContext }
        }
    }
    Throw-StrictJsonError $script:StrictJsonContext
}

function Read-StrictJsonLiteral([string]$Literal) {
    if ($script:StrictJsonIndex + $Literal.Length -gt $script:StrictJsonText.Length -or
        -not [string]::Equals(
            $script:StrictJsonText.Substring($script:StrictJsonIndex, $Literal.Length),
            $Literal,
            [StringComparison]::Ordinal)) {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $script:StrictJsonIndex += $Literal.Length
}

function Read-StrictJsonNumber {
    $remaining = $script:StrictJsonText.Substring($script:StrictJsonIndex)
    $match = [regex]::Match(
        $remaining,
        '^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?')
    if (-not $match.Success) {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $script:StrictJsonIndex += $match.Length
}

function Read-StrictJsonArray {
    $script:StrictJsonIndex++
    Skip-StrictJsonWhitespace
    if ($script:StrictJsonIndex -lt $script:StrictJsonText.Length -and
        $script:StrictJsonText[$script:StrictJsonIndex] -eq ']') {
        $script:StrictJsonIndex++
        return
    }
    while ($true) {
        Read-StrictJsonValue
        Skip-StrictJsonWhitespace
        if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length) {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        $separator = $script:StrictJsonText[$script:StrictJsonIndex]
        $script:StrictJsonIndex++
        if ($separator -eq ']') {
            return
        }
        if ($separator -ne ',') {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        Skip-StrictJsonWhitespace
    }
}

function Read-StrictJsonObject {
    $script:StrictJsonIndex++
    $exactNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $caseInsensitiveNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    Skip-StrictJsonWhitespace
    if ($script:StrictJsonIndex -lt $script:StrictJsonText.Length -and
        $script:StrictJsonText[$script:StrictJsonIndex] -eq '}') {
        $script:StrictJsonIndex++
        return
    }
    while ($true) {
        $name = Read-StrictJsonString
        if (-not $exactNames.Add($name) -or -not $caseInsensitiveNames.Add($name)) {
            throw "Duplicate or case-colliding JSON member in $script:StrictJsonContext."
        }
        Skip-StrictJsonWhitespace
        if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length -or
            $script:StrictJsonText[$script:StrictJsonIndex] -ne ':') {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        $script:StrictJsonIndex++
        Read-StrictJsonValue
        Skip-StrictJsonWhitespace
        if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length) {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        $separator = $script:StrictJsonText[$script:StrictJsonIndex]
        $script:StrictJsonIndex++
        if ($separator -eq '}') {
            return
        }
        if ($separator -ne ',') {
            Throw-StrictJsonError $script:StrictJsonContext
        }
        Skip-StrictJsonWhitespace
    }
}

function Read-StrictJsonValue {
    Skip-StrictJsonWhitespace
    if ($script:StrictJsonIndex -ge $script:StrictJsonText.Length) {
        Throw-StrictJsonError $script:StrictJsonContext
    }
    $character = $script:StrictJsonText[$script:StrictJsonIndex]
    switch ($character) {
        '{' { Read-StrictJsonObject }
        '[' { Read-StrictJsonArray }
        '"' { [void](Read-StrictJsonString) }
        't' { Read-StrictJsonLiteral 'true' }
        'f' { Read-StrictJsonLiteral 'false' }
        'n' { Read-StrictJsonLiteral 'null' }
        default {
            if ($character -eq '-' -or [char]::IsDigit($character)) {
                Read-StrictJsonNumber
            }
            else {
                Throw-StrictJsonError $script:StrictJsonContext
            }
        }
    }
}

function ConvertFrom-StrictJson([string]$Text, [string]$Context) {
    $script:StrictJsonText = $Text
    $script:StrictJsonIndex = 0
    $script:StrictJsonContext = $Context
    Read-StrictJsonValue
    Skip-StrictJsonWhitespace
    if ($script:StrictJsonIndex -ne $script:StrictJsonText.Length) {
        Throw-StrictJsonError $Context
    }
    try {
        return $Text | ConvertFrom-Json
    }
    catch {
        $protectedNumbers = Protect-JsonNumberTokens $Text $true
        if ($protectedNumbers.Tokens.Count -eq 0) {
            Throw-StrictJsonError $Context
        }
        try {
            return $protectedNumbers.Text | ConvertFrom-Json
        }
        catch {
            Throw-StrictJsonError $Context
        }
    }
}

function Read-StrictJsonFile([string]$Path, [string]$Context) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing $Context file: $Path"
    }
    $text = Read-Utf8TextStrict $Path $Context
    return ConvertFrom-StrictJson $text $Context
}

function Assert-JsonObject([object]$Value, [string]$Context) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) {
        throw "$Context must be a JSON object."
    }
}

function Get-ExactJsonProperties([object]$Object) {
    Assert-JsonObject $Object 'JSON value'
    return @($Object.PSObject.Properties | Where-Object { $_.MemberType -eq 'NoteProperty' })
}

function Get-RequiredJsonProperty([object]$Object, [string]$Name, [string]$Context) {
    Assert-JsonObject $Object $Context
    $matches = @(
        Get-ExactJsonProperties $Object |
            Where-Object { [string]::Equals($_.Name, $Name, [StringComparison]::Ordinal) }
    )
    if ($matches.Count -ne 1) {
        throw "$Context requires exact JSON member '$Name'."
    }
    return $matches[0]
}

function Get-OptionalJsonProperty([object]$Object, [string]$Name, [string]$Context) {
    Assert-JsonObject $Object $Context
    $caseInsensitiveMatches = @(
        Get-ExactJsonProperties $Object |
            Where-Object { [string]::Equals($_.Name, $Name, [StringComparison]::OrdinalIgnoreCase) }
    )
    $matches = @(
        $caseInsensitiveMatches |
            Where-Object { [string]::Equals($_.Name, $Name, [StringComparison]::Ordinal) }
    )
    if ($caseInsensitiveMatches.Count -ne $matches.Count) {
        throw "$Context contains wrong-case JSON member '$Name'."
    }
    if ($matches.Count -eq 0) {
        return $null
    }
    return $matches[0]
}

function Assert-AllowedJsonMembers([object]$Object, [string[]]$Allowed, [string]$Context) {
    Assert-JsonObject $Object $Context
    foreach ($property in Get-ExactJsonProperties $Object) {
        $isAllowed = $false
        foreach ($allowedName in $Allowed) {
            if ([string]::Equals($property.Name, $allowedName, [StringComparison]::Ordinal)) {
                $isAllowed = $true
                break
            }
        }
        if (-not $isAllowed) {
            throw "$Context contains unsupported JSON member '$($property.Name)'."
        }
    }
}

function Assert-JsonString([object]$Value, [string]$Context) {
    if ($Value -isnot [string]) {
        throw "$Context must be a JSON string."
    }
}

function Assert-JsonInteger([object]$Value, [string]$Context) {
    if ($Value -isnot [int] -and $Value -isnot [long]) {
        throw "$Context must be a JSON integer."
    }
}

function Assert-JsonBoolean([object]$Value, [string]$Context) {
    if ($Value -isnot [bool]) {
        throw "$Context must be a JSON boolean."
    }
}

function Assert-JsonStringArray([object]$Value, [string]$Context) {
    if ($Value -isnot [System.Array]) {
        throw "$Context must be a JSON array."
    }
    foreach ($item in $Value) {
        Assert-JsonString $item "$Context item"
    }
}

function Test-OrdinalStringArrayEqual([object]$Value, [string[]]$Expected) {
    if ($Value -isnot [System.Array]) {
        return $false
    }
    $actual = @($Value)
    if ($actual.Count -ne $Expected.Count) {
        return $false
    }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($actual[$index] -isnot [string] -or
            -not [string]::Equals($actual[$index], $Expected[$index], [StringComparison]::Ordinal)) {
            return $false
        }
    }
    return $true
}

function Get-NormalizedUnityMcpUrl([string]$Url) {
    $uri = $null
    if ([string]::IsNullOrWhiteSpace($Url) -or
        -not [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$uri) -or
        -not [string]::Equals($uri.Scheme, 'http', [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($uri.Host, '127.0.0.1', [StringComparison]::Ordinal) -or
        $uri.UserInfo -or
        $uri.Port -lt 1 -or
        $uri.Port -gt 65535 -or
        -not [string]::Equals($uri.AbsolutePath.TrimEnd('/'), '/mcp', [StringComparison]::Ordinal) -or
        $uri.Query -or
        $uri.Fragment) {
        throw 'Unity MCP URL must be a loopback HTTP /mcp endpoint without credentials, query, or fragment.'
    }
    return $uri.GetLeftPart([UriPartial]::Authority).TrimEnd('/') + '/mcp'
}

function Get-UnityMcpUrlFromDocument(
    [object]$Document,
    [string]$Context,
    [string[]]$AdditionalRootMembers = @()) {
    $allowedRootMembers = @('mcpServers') + @($AdditionalRootMembers)
    Assert-AllowedJsonMembers $Document $allowedRootMembers $Context
    $servers = (Get-RequiredJsonProperty $Document 'mcpServers' $Context).Value
    Assert-AllowedJsonMembers $servers @('unityMCP') "$Context.mcpServers"
    $unityMcp = (Get-RequiredJsonProperty $servers 'unityMCP' "$Context.mcpServers").Value
    Assert-AllowedJsonMembers $unityMcp @('url') "$Context.mcpServers.unityMCP"
    $url = (Get-RequiredJsonProperty $unityMcp 'url' "$Context.mcpServers.unityMCP").Value
    Assert-JsonString $url "$Context.mcpServers.unityMCP.url"
    return Get-NormalizedUnityMcpUrl $url
}

function Read-UnityMcpUrl([string]$Path, [string]$Context) {
    $document = Read-StrictJsonFile $Path $Context
    return Get-UnityMcpUrlFromDocument $document $Context
}

function Assert-ProjectClientDocument(
    [object]$Document,
    [string]$SchemaUrl,
    [string]$ExpectedUrl,
    [bool]$RequireTimeout,
    [string]$Context) {
    Assert-AllowedJsonMembers $Document @('$schema', 'plugin', 'mcp', 'lsp') $Context
    $schema = (Get-RequiredJsonProperty $Document '$schema' $Context).Value
    Assert-JsonString $schema "$Context.`$schema"
    if (-not [string]::Equals($schema, $SchemaUrl, [StringComparison]::Ordinal)) {
        throw "$Context has an unexpected schema."
    }
    $plugins = (Get-RequiredJsonProperty $Document 'plugin' $Context).Value
    Assert-JsonStringArray $plugins "$Context.plugin"
    if (-not (Test-OrdinalStringArrayEqual $plugins @('./.opencode/plugin/auto-compile.js'))) {
        throw "$Context has an unexpected plugin list."
    }
    $mcp = (Get-RequiredJsonProperty $Document 'mcp' $Context).Value
    Assert-AllowedJsonMembers $mcp @('unity-MCP') "$Context.mcp"
    $unityMcp = (Get-RequiredJsonProperty $mcp 'unity-MCP' "$Context.mcp").Value
    $allowedUnityFields = @('type', 'url')
    if ($RequireTimeout) {
        $allowedUnityFields += 'timeout'
    }
    Assert-AllowedJsonMembers $unityMcp $allowedUnityFields "$Context.mcp.unity-MCP"
    $type = (Get-RequiredJsonProperty $unityMcp 'type' "$Context.mcp.unity-MCP").Value
    $url = (Get-RequiredJsonProperty $unityMcp 'url' "$Context.mcp.unity-MCP").Value
    Assert-JsonString $type "$Context.mcp.unity-MCP.type"
    Assert-JsonString $url "$Context.mcp.unity-MCP.url"
    if (-not [string]::Equals($type, 'remote', [StringComparison]::Ordinal) -or
        -not [string]::Equals($url, $ExpectedUrl, [StringComparison]::Ordinal)) {
        throw "$Context has unexpected Unity MCP settings."
    }
    if ($RequireTimeout) {
        $timeout = (Get-RequiredJsonProperty $unityMcp 'timeout' "$Context.mcp.unity-MCP").Value
        Assert-JsonInteger $timeout "$Context.mcp.unity-MCP.timeout"
        if ($timeout -ne 300000) {
            throw "$Context has an unexpected timeout."
        }
    }
    $lsp = (Get-RequiredJsonProperty $Document 'lsp' $Context).Value
    Assert-AllowedJsonMembers $lsp @('csharp') "$Context.lsp"
    $csharp = (Get-RequiredJsonProperty $lsp 'csharp' "$Context.lsp").Value
    Assert-AllowedJsonMembers $csharp @('command', 'extensions') "$Context.lsp.csharp"
    $command = (Get-RequiredJsonProperty $csharp 'command' "$Context.lsp.csharp").Value
    $extensions = (Get-RequiredJsonProperty $csharp 'extensions' "$Context.lsp.csharp").Value
    if (-not (Test-OrdinalStringArrayEqual $command @('roslyn-language-server', '--stdio')) -or
        -not (Test-OrdinalStringArrayEqual $extensions @('.cs'))) {
        throw "$Context has unexpected C# LSP settings."
    }
}

function Get-ValidatedJsonTemplateContent(
    [string]$Path,
    [string]$SchemaUrl,
    [string]$Url,
    [bool]$RequireTimeout,
    [string]$Context) {
    $template = Read-Utf8TextStrict $Path $Context
    $placeholderCount = [regex]::Matches(
        $template,
        [regex]::Escape('__UNITY_MCP_URL__')).Count
    if ($placeholderCount -ne 1) {
        throw "$Context must contain __UNITY_MCP_URL__ exactly once."
    }
    $templateDocument = ConvertFrom-StrictJson $template $Context
    Assert-ProjectClientDocument `
        $templateDocument `
        $SchemaUrl `
        '__UNITY_MCP_URL__' `
        $RequireTimeout `
        $Context
    $rendered = $template.Replace('__UNITY_MCP_URL__', $Url)
    $renderedDocument = ConvertFrom-StrictJson $rendered "$Context rendered"
    Assert-ProjectClientDocument `
        $renderedDocument `
        $SchemaUrl `
        $Url `
        $RequireTimeout `
        "$Context rendered"
    return $rendered
}

function Assert-CodexContent([string]$Content, [string]$ExpectedUrl, [string]$Context) {
    $expected = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    $expected['features'] = [ordered]@{ rmcp_client = 'true' }
    $expected['mcp_servers.unityMCP'] = [ordered]@{ url = '"' + $ExpectedUrl + '"' }
    $expected['mcp_servers.unityMCP.tools.set_active_instance'] = [ordered]@{ approval_mode = '"approve"' }
    $expected['mcp_servers.unityMCP.tools.refresh_unity'] = [ordered]@{ approval_mode = '"approve"' }
    $expected['mcp_servers.unityMCP.tools.read_console'] = [ordered]@{ approval_mode = '"approve"' }

    $seenSections = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $seenSectionsIgnoreCase = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $seenKeys = @{}
    $currentSection = $null
    foreach ($line in [regex]::Split($Content, '\r?\n')) {
        if ($line -match '^[\t ]*$') {
            continue
        }
        if ($line -match '^\[([A-Za-z0-9_.-]+)\]$') {
            $section = $matches[1]
            if (-not $expected.ContainsKey($section) -or
                -not $seenSections.Add($section) -or
                -not $seenSectionsIgnoreCase.Add($section)) {
                throw "$Context contains an invalid or duplicate TOML section."
            }
            $currentSection = $section
            $seenKeys[$section] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
            continue
        }
        if ($null -eq $currentSection -or $line -notmatch '^([A-Za-z0-9_-]+)[\t ]*=[\t ]*(.+)$') {
            throw "$Context contains unsupported TOML syntax."
        }
        $key = $matches[1]
        $value = $matches[2] -replace '^[\t ]+|[\t ]+$', ''
        $sectionExpected = $expected[$currentSection]
        $expectedKey = $null
        foreach ($candidateKey in $sectionExpected.Keys) {
            if ([string]::Equals([string]$candidateKey, $key, [StringComparison]::Ordinal)) {
                $expectedKey = [string]$candidateKey
                break
            }
        }
        if ($null -eq $expectedKey -or -not $seenKeys[$currentSection].Add($key)) {
            throw "$Context contains an invalid or duplicate TOML key."
        }
        if (-not [string]::Equals($value, [string]$sectionExpected[$expectedKey], [StringComparison]::Ordinal)) {
            throw "$Context contains an unexpected TOML value."
        }
    }
    if ($seenSections.Count -ne $expected.Count) {
        throw "$Context is missing a required TOML section."
    }
    foreach ($section in $expected.Keys) {
        if ($seenKeys[$section].Count -ne $expected[$section].Count) {
            throw "$Context is missing a required TOML key."
        }
    }
}

function Get-CodexRenderedContent([string]$Path, [string]$Url) {
    $template = Read-Utf8TextStrict $Path 'Codex template'
    if ([regex]::Matches($template, [regex]::Escape('__UNITY_MCP_URL__')).Count -ne 1) {
        throw 'Codex template must contain __UNITY_MCP_URL__ exactly once.'
    }
    Assert-CodexContent $template '__UNITY_MCP_URL__' 'Codex template'
    $rendered = $template.Replace('__UNITY_MCP_URL__', $Url)
    Assert-CodexContent $rendered $Url 'Codex rendered configuration'
    return $rendered
}

function Test-ProjectClientConfig(
    [object]$Document,
    [string]$Url,
    [string]$SchemaUrl,
    [bool]$RequireTimeout,
    [string]$Context) {
    try {
        Assert-JsonObject $Document $Context
        $schemaProperty = Get-OptionalJsonProperty $Document '$schema' $Context
        $pluginProperty = Get-OptionalJsonProperty $Document 'plugin' $Context
        $mcpProperty = Get-OptionalJsonProperty $Document 'mcp' $Context
        $lspProperty = Get-OptionalJsonProperty $Document 'lsp' $Context
        if ($null -eq $schemaProperty -or $null -eq $pluginProperty -or
            $null -eq $mcpProperty -or $null -eq $lspProperty) {
            return $false
        }
        Assert-JsonString $schemaProperty.Value "$Context.`$schema"
        Assert-JsonStringArray $pluginProperty.Value "$Context.plugin"
        Assert-JsonObject $mcpProperty.Value "$Context.mcp"
        Assert-JsonObject $lspProperty.Value "$Context.lsp"
        $unityProperty = Get-OptionalJsonProperty $mcpProperty.Value 'unity-MCP' "$Context.mcp"
        $csharpProperty = Get-OptionalJsonProperty $lspProperty.Value 'csharp' "$Context.lsp"
        if ($null -eq $unityProperty -or $null -eq $csharpProperty) {
            return $false
        }
        $unity = $unityProperty.Value
        $csharp = $csharpProperty.Value
        Assert-JsonObject $unity "$Context.mcp.unity-MCP"
        Assert-JsonObject $csharp "$Context.lsp.csharp"
        $type = (Get-RequiredJsonProperty $unity 'type' "$Context.mcp.unity-MCP").Value
        $actualUrl = (Get-RequiredJsonProperty $unity 'url' "$Context.mcp.unity-MCP").Value
        $command = (Get-RequiredJsonProperty $csharp 'command' "$Context.lsp.csharp").Value
        $extensions = (Get-RequiredJsonProperty $csharp 'extensions' "$Context.lsp.csharp").Value
        Assert-JsonString $type "$Context type"
        Assert-JsonString $actualUrl "$Context URL"
        $timeoutIsCurrent = $true
        if ($RequireTimeout) {
            $timeout = (Get-RequiredJsonProperty $unity 'timeout' "$Context.mcp.unity-MCP").Value
            Assert-JsonInteger $timeout "$Context timeout"
            $timeoutIsCurrent = $timeout -eq 300000
        }
        return [string]::Equals($schemaProperty.Value, $SchemaUrl, [StringComparison]::Ordinal) -and
            './.opencode/plugin/auto-compile.js' -cin @($pluginProperty.Value) -and
            [string]::Equals($type, 'remote', [StringComparison]::Ordinal) -and
            [string]::Equals($actualUrl, $Url, [StringComparison]::Ordinal) -and
            $timeoutIsCurrent -and
            (Test-OrdinalStringArrayEqual $command @('roslyn-language-server', '--stdio')) -and
            (Test-OrdinalStringArrayEqual $extensions @('.cs'))
    }
    catch {
        throw
    }
}

function Test-PowerShellJsonNumberSupported([string]$Lexeme) {
    try {
        [void](('{"value":' + $Lexeme + '}') | ConvertFrom-Json)
        return $true
    }
    catch {
        return $false
    }
}

function Protect-JsonNumberTokens(
    [string]$Text,
    [bool]$OnlyPowerShellUnsupported = $false) {
    $prefix = '__TACTICS_JSON_NUMBER_' + [Guid]::NewGuid().ToString('N') + '_'
    while ($Text.Contains($prefix)) {
        $prefix = '__TACTICS_JSON_NUMBER_' + [Guid]::NewGuid().ToString('N') + '_'
    }
    $builder = New-Object System.Text.StringBuilder
    $tokens = New-Object System.Collections.ArrayList
    $index = 0
    $insideString = $false
    $escaped = $false
    while ($index -lt $Text.Length) {
        $character = $Text[$index]
        if ($insideString) {
            [void]$builder.Append($character)
            if ($escaped) {
                $escaped = $false
            }
            elseif ($character -eq '\') {
                $escaped = $true
            }
            elseif ($character -eq '"') {
                $insideString = $false
            }
            $index++
            continue
        }
        if ($character -eq '"') {
            $insideString = $true
            [void]$builder.Append($character)
            $index++
            continue
        }
        if ($character -eq '-' -or [char]::IsDigit($character)) {
            $match = [regex]::Match(
                $Text.Substring($index),
                '^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?')
            if ($match.Success) {
                if ($OnlyPowerShellUnsupported -and
                    (Test-PowerShellJsonNumberSupported $match.Value)) {
                    [void]$builder.Append($match.Value)
                    $index += $match.Length
                    continue
                }
                $sentinel = $prefix + $tokens.Count + '__'
                [void]$tokens.Add([pscustomobject]@{
                        Sentinel = $sentinel
                        Lexeme = $match.Value
                    })
                [void]$builder.Append('"').Append($sentinel).Append('"')
                $index += $match.Length
                continue
            }
        }
        [void]$builder.Append($character)
        $index++
    }
    return [pscustomobject]@{
        Text = $builder.ToString()
        Tokens = @($tokens)
    }
}

function Restore-JsonNumberTokens([string]$Text, [object[]]$Tokens) {
    $result = $Text
    foreach ($token in $Tokens) {
        $quotedSentinel = '"' + $token.Sentinel + '"'
        $occurrences = [regex]::Matches(
            $result,
            [regex]::Escape($quotedSentinel)).Count
        if ($occurrences -gt 1) {
            throw 'Protected JSON number sentinel appeared more than once.'
        }
        if ($occurrences -eq 1) {
            $result = $result.Replace($quotedSentinel, $token.Lexeme)
        }
    }
    return $result
}

function Get-ProjectClientPlan(
    [string]$TemplatePath,
    [string]$OutputPath,
    [string]$Url,
    [string]$SchemaUrl,
    [bool]$RequireTimeout,
    [string]$Context,
    [object]$BaseContent) {
    $templateContent = Get-ValidatedJsonTemplateContent `
        $TemplatePath `
        $SchemaUrl `
        $Url `
        $RequireTimeout `
        "$Context template"
    $actualContent = if (Test-Path -LiteralPath $OutputPath -PathType Leaf) {
        Read-Utf8TextStrict $OutputPath "$Context configuration"
    }
    else {
        $null
    }
    if ($null -ne $BaseContent -and $BaseContent -isnot [string]) {
        throw "$Context migration base must be a JSON string."
    }
    $contentToMerge = if ($null -ne $BaseContent) { [string]$BaseContent } else { $actualContent }
    if ($null -eq $contentToMerge) {
        return [pscustomobject]@{ Changed = $true; Content = $templateContent }
    }
    $originalDocument = ConvertFrom-StrictJson $contentToMerge "$Context configuration"
    if ((Test-ProjectClientConfig $originalDocument $Url $SchemaUrl $RequireTimeout "$Context configuration") -and
        [string]::Equals($actualContent, $contentToMerge, [StringComparison]::Ordinal)) {
        return [pscustomobject]@{ Changed = $false; Content = $null }
    }

    $protectedNumbers = Protect-JsonNumberTokens $contentToMerge
    $document = ConvertFrom-StrictJson `
        $protectedNumbers.Text `
        "$Context number-protected configuration"

    $configurationContext = "$Context configuration"
    $schemaProperty = Get-OptionalJsonProperty $document '$schema' $configurationContext
    if ($null -eq $schemaProperty) {
        $document | Add-Member -MemberType NoteProperty -Name '$schema' -Value $SchemaUrl
    }
    else {
        $schemaProperty.Value = $SchemaUrl
    }
    $pluginProperty = Get-OptionalJsonProperty $document 'plugin' $configurationContext
    if ($null -eq $pluginProperty) {
        $document | Add-Member -MemberType NoteProperty -Name 'plugin' -Value @('./.opencode/plugin/auto-compile.js')
    }
    else {
        Assert-JsonStringArray $pluginProperty.Value "$configurationContext.plugin"
        $plugins = @($pluginProperty.Value)
        if ('./.opencode/plugin/auto-compile.js' -cnotin $plugins) {
            $plugins += './.opencode/plugin/auto-compile.js'
        }
        $pluginProperty.Value = $plugins
    }
    $mcpProperty = Get-OptionalJsonProperty $document 'mcp' $configurationContext
    if ($null -eq $mcpProperty) {
        $document | Add-Member -MemberType NoteProperty -Name 'mcp' -Value ([pscustomobject]@{})
        $mcpProperty = Get-RequiredJsonProperty $document 'mcp' $configurationContext
    }
    Assert-JsonObject $mcpProperty.Value "$configurationContext.mcp"
    $unityProperty = Get-OptionalJsonProperty $mcpProperty.Value 'unity-MCP' "$configurationContext.mcp"
    if ($null -eq $unityProperty) {
        $mcpProperty.Value | Add-Member -MemberType NoteProperty -Name 'unity-MCP' -Value ([pscustomobject]@{})
        $unityProperty = Get-RequiredJsonProperty $mcpProperty.Value 'unity-MCP' "$configurationContext.mcp"
    }
    Assert-JsonObject $unityProperty.Value "$configurationContext.mcp.unity-MCP"
    $unitySettings = @(
        @{ Name = 'type'; Value = 'remote' },
        @{ Name = 'url'; Value = $Url })
    if ($RequireTimeout) {
        $unitySettings += @{ Name = 'timeout'; Value = 300000 }
    }
    foreach ($setting in $unitySettings) {
        $property = Get-OptionalJsonProperty $unityProperty.Value $setting.Name "$configurationContext.mcp.unity-MCP"
        if ($null -eq $property) {
            $unityProperty.Value | Add-Member -MemberType NoteProperty -Name $setting.Name -Value $setting.Value
        }
        else {
            $property.Value = $setting.Value
        }
    }
    $lspProperty = Get-OptionalJsonProperty $document 'lsp' $configurationContext
    if ($null -eq $lspProperty) {
        $document | Add-Member -MemberType NoteProperty -Name 'lsp' -Value ([pscustomobject]@{})
        $lspProperty = Get-RequiredJsonProperty $document 'lsp' $configurationContext
    }
    Assert-JsonObject $lspProperty.Value "$configurationContext.lsp"
    $csharpProperty = Get-OptionalJsonProperty $lspProperty.Value 'csharp' "$configurationContext.lsp"
    if ($null -eq $csharpProperty) {
        $lspProperty.Value | Add-Member -MemberType NoteProperty -Name 'csharp' -Value ([pscustomobject]@{})
        $csharpProperty = Get-RequiredJsonProperty $lspProperty.Value 'csharp' "$configurationContext.lsp"
    }
    Assert-JsonObject $csharpProperty.Value "$configurationContext.lsp.csharp"
    foreach ($setting in @(
            @{ Name = 'command'; Value = @('roslyn-language-server', '--stdio') },
            @{ Name = 'extensions'; Value = @('.cs') })) {
        $property = Get-OptionalJsonProperty $csharpProperty.Value $setting.Name "$configurationContext.lsp.csharp"
        if ($null -eq $property) {
            $csharpProperty.Value | Add-Member -MemberType NoteProperty -Name $setting.Name -Value $setting.Value
        }
        else {
            $property.Value = $setting.Value
        }
    }
    $content = $document | ConvertTo-Json -Depth 100
    $content = Restore-JsonNumberTokens $content $protectedNumbers.Tokens
    $roundTrip = ConvertFrom-StrictJson $content "$Context generated configuration"
    if (-not (Test-ProjectClientConfig $roundTrip $Url $SchemaUrl $RequireTimeout "$Context generated configuration")) {
        throw "$Context generated configuration failed validation."
    }
    return [pscustomobject]@{ Changed = $true; Content = $content }
}

function Invoke-WithConfigLock([string]$Path, [scriptblock]$Action) {
    Write-TestLockAttemptMarker
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing tracked MCP configuration lock anchor: $Path"
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $lockStream = $null
    try {
        while ($null -eq $lockStream) {
            try {
                $lockStream = [System.IO.File]::Open(
                    $Path,
                    [System.IO.FileMode]::Open,
                    [System.IO.FileAccess]::Read,
                    [System.IO.FileShare]::None)
            }
            catch [System.IO.IOException] {
                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "Timed out waiting for project MCP configuration lock: $Path"
                }
                Start-Sleep -Milliseconds 50
            }
        }
        return & $Action
    }
    finally {
        if ($null -ne $lockStream) {
            $lockStream.Dispose()
        }
    }
}

function Write-TestLockAttemptMarker {
    $testMode = [Environment]::GetEnvironmentVariable('TACTICS_TEST_MCP_CONFIG')
    $markerPath = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_LOCK_ATTEMPT_MARKER')
    if ([string]::Equals($testMode, '1', [StringComparison]::Ordinal) -and
        -not [string]::IsNullOrWhiteSpace($markerPath)) {
        [System.IO.File]::WriteAllText($markerPath, 'attempting-lock')
    }
}

function New-ToolSidecarPath([string]$Path, [string]$Extension) {
    return "$Path.$PID.$([Guid]::NewGuid().ToString('N')).$Extension"
}

function Remove-BestEffort([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Set-FileBytes([string]$Path, [byte[]]$Value) {
    $directory = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporaryPath = New-ToolSidecarPath $Path 'tmp'
    $backupPath = New-ToolSidecarPath $Path 'bak'
    $committed = $false
    try {
        [System.IO.File]::WriteAllBytes($temporaryPath, $Value)
        if (Test-Path -LiteralPath $Path) {
            [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
        }
        else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
        $committed = $true
    }
    finally {
        Remove-BestEffort $temporaryPath
        if ($committed) {
            Remove-BestEffort $backupPath
        }
    }
}

function Set-Utf8Text([string]$Path, [string]$Value) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    Set-FileBytes $Path $encoding.GetBytes($Value)
}

function Get-ToolOwnedResidualFiles([string[]]$Paths) {
    $results = @()
    foreach ($path in $Paths) {
        $directory = Split-Path -Parent $path
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            continue
        }
        $leaf = Split-Path -Leaf $path
        $pattern = '^' + [regex]::Escape($leaf) + '\.[1-9][0-9]*\.[0-9A-Fa-f]{32}\.(?:tmp|bak)$'
        foreach ($candidate in Get-ChildItem -LiteralPath $directory -File) {
            if ($candidate.Name -cmatch $pattern) {
                $results += $candidate.FullName
            }
        }
    }
    return $results
}

function Remove-ToolOwnedResidualFiles([string[]]$Paths) {
    foreach ($residual in Get-ToolOwnedResidualFiles $Paths) {
        Remove-Item -LiteralPath $residual -Force
    }
}

function Get-FaultInjectionPosition([int]$OperationCount) {
    $testMode = [Environment]::GetEnvironmentVariable('TACTICS_TEST_MCP_CONFIG')
    $text = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS')
    if (-not [string]::Equals($testMode, '1', [StringComparison]::Ordinal)) {
        return 0
    }
    if ([string]::IsNullOrWhiteSpace($text)) {
        return 0
    }
    $position = 0
    if (-not [int]::TryParse($text, [ref]$position) -or
        $position -lt 1 -or
        $position -gt $OperationCount) {
        throw 'Invalid test-only MCP configuration fault injection position.'
    }
    return $position
}

function Get-FileSetFingerprint([string[]]$Paths) {
    $sortedPaths = @($Paths | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
    [Array]::Sort($sortedPaths, [StringComparer]::Ordinal)
    $builder = New-Object System.Text.StringBuilder
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($path in $sortedPaths) {
            [void]$builder.Append($path.Length).Append(':').Append($path).Append('|')
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $fileHash = $sha256.ComputeHash([System.IO.File]::ReadAllBytes($path))
                [void]$builder.Append(
                    -join ($fileHash | ForEach-Object { $_.ToString('x2') }))
            }
            else {
                [void]$builder.Append('<missing>')
            }
            [void]$builder.Append("`n")
        }
        $setHash = $sha256.ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes($builder.ToString()))
        return -join ($setHash | ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-TransactionTargets([object[]]$Operations) {
    Assert-ManagedPathsAreFilesOrAbsent @($Operations | ForEach-Object { $_.Path })
}

function Assert-ManagedPathsAreFilesOrAbsent([string[]]$Paths) {
    foreach ($path in $Paths) {
        if (Test-Path -LiteralPath $path) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Managed MCP configuration target exists but is not a file: $path"
            }
        }
    }
}

function Invoke-FileTransaction([object[]]$Operations, [int]$FailAfter) {
    Assert-TransactionTargets $Operations
    $snapshots = @{}
    foreach ($operation in $Operations) {
        if (-not $snapshots.ContainsKey($operation.Path)) {
            $isFile = Test-Path -LiteralPath $operation.Path -PathType Leaf
            $snapshots[$operation.Path] = [pscustomobject]@{
                State = if ($isFile) { 'File' } else { 'Absent' }
                Bytes = if ($isFile) { [System.IO.File]::ReadAllBytes($operation.Path) } else { $null }
            }
        }
    }
    $attemptedPaths = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($operation in $Operations) {
            $attemptedPaths.Add($operation.Path)
            switch ($operation.Type) {
                'WriteText' {
                    Set-Utf8Text $operation.Path $operation.Content
                    Invoke-TestInternalOperationFailure 'WriteText'
                }
                'Delete' {
                    if (Test-Path -LiteralPath $operation.Path -PathType Leaf) {
                        Remove-Item -LiteralPath $operation.Path -Force
                    }
                    Invoke-TestInternalOperationFailure 'Delete'
                }
                default { throw "Unsupported MCP configuration transaction operation: $($operation.Type)" }
            }
            Wait-TestTransactionBarrier $attemptedPaths.Count
            if ($FailAfter -gt 0 -and $attemptedPaths.Count -eq $FailAfter) {
                throw "Injected MCP configuration transaction failure after $FailAfter operations."
            }
        }
    }
    catch {
        $writeError = $_
        $rollbackErrors = @()
        for ($index = $attemptedPaths.Count - 1; $index -ge 0; $index--) {
            $path = $attemptedPaths[$index]
            $snapshot = $snapshots[$path]
            try {
                if ($snapshot.State -eq 'File') {
                    Set-FileBytes $path $snapshot.Bytes
                }
                elseif (Test-Path -LiteralPath $path) {
                    if (Test-Path -LiteralPath $path -PathType Leaf) {
                        Remove-Item -LiteralPath $path -Force
                    }
                    else {
                        throw "Rollback refused to remove non-file path: $path"
                    }
                }
            }
            catch {
                $rollbackErrors += $_.Exception.Message
            }
        }
        if ($rollbackErrors.Count -gt 0) {
            throw "$($writeError.Exception.Message) Rollback also failed: $($rollbackErrors -join '; ')"
        }
        throw $writeError
    }
}

function Invoke-TestInternalOperationFailure([string]$OperationType) {
    if ($script:TestInternalOperationFailureInjected) {
        return
    }
    $testMode = [Environment]::GetEnvironmentVariable('TACTICS_TEST_MCP_CONFIG')
    $requestedType = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_FAIL_DURING_OPERATION')
    if ([string]::Equals($testMode, '1', [StringComparison]::Ordinal) -and
        [string]::Equals($requestedType, $OperationType, [StringComparison]::Ordinal)) {
        $script:TestInternalOperationFailureInjected = $true
        throw "Injected MCP configuration failure during $OperationType operation."
    }
}

function Wait-TestTransactionBarrier([int]$OperationCount) {
    $testMode = [Environment]::GetEnvironmentVariable('TACTICS_TEST_MCP_CONFIG')
    if (-not [string]::Equals($testMode, '1', [StringComparison]::Ordinal)) {
        return
    }
    $afterText = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_BARRIER_AFTER_OPERATION')
    if ([string]::IsNullOrEmpty($afterText)) {
        return
    }
    $after = 0
    if (-not [int]::TryParse($afterText, [ref]$after) -or $after -lt 1) {
        throw 'Invalid test-only MCP configuration barrier position.'
    }
    if ($OperationCount -ne $after) {
        return
    }
    $markerPath = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_BARRIER_MARKER')
    $releasePath = [Environment]::GetEnvironmentVariable(
        'TACTICS_TEST_MCP_CONFIG_BARRIER_RELEASE')
    if ([string]::IsNullOrWhiteSpace($markerPath) -or
        [string]::IsNullOrWhiteSpace($releasePath)) {
        throw 'Test-only MCP configuration barrier paths are required.'
    }
    [System.IO.File]::WriteAllText($markerPath, 'ready')
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (Test-Path -LiteralPath $releasePath -PathType Leaf)) {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw 'Timed out waiting for test-only MCP configuration barrier release.'
        }
        Start-Sleep -Milliseconds 25
    }
}

function New-WriteOperation([string]$Path, [string]$Content) {
    return [pscustomobject]@{ Type = 'WriteText'; Path = $Path; Content = $Content }
}

function New-DeleteOperation([string]$Path) {
    return [pscustomobject]@{ Type = 'Delete'; Path = $Path; Content = $null }
}

function Add-TextWriteIfChanged([System.Collections.ArrayList]$Operations, [string]$Path, [string]$Content) {
    $actual = if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Read-Utf8TextStrict $Path 'generated MCP configuration'
    }
    else {
        $null
    }
    if (-not [string]::Equals($actual, $Content, [StringComparison]::Ordinal)) {
        [void]$Operations.Add((New-WriteOperation $Path $Content))
    }
}

function Get-SourceContent([string]$Url) {
    $document = [ordered]@{
        mcpServers = [ordered]@{
            unityMCP = [ordered]@{ url = $Url }
        }
    }
    return $document | ConvertTo-Json -Depth 10
}

function Get-OptionalLocalConfigContent(
    [string]$ProjectRoot,
    [string]$RelativePath,
    [string]$Context) {
    $path = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return $null
    }
    $content = Read-Utf8TextStrict $path $Context
    $document = ConvertFrom-StrictJson $content $Context
    Assert-JsonObject $document $Context
    return $content
}

function Get-MigrationBackupContent([string]$ProjectRoot, [string]$Url) {
    $document = [ordered]@{
        version = 1
        mcpServers = [ordered]@{
            unityMCP = [ordered]@{ url = $Url }
        }
        localConfigurations = [ordered]@{
            opencode = Get-OptionalLocalConfigContent `
                $ProjectRoot '.opencode/opencode.json' 'OpenCode migration source'
            mimocode = Get-OptionalLocalConfigContent `
                $ProjectRoot '.mimocode/mimocode.json' 'MiMoCode migration source'
        }
    }
    return $document | ConvertTo-Json -Depth 10
}

function Read-MigrationBackup([string]$Path) {
    $document = Read-StrictJsonFile $Path 'migration backup'
    $url = Get-UnityMcpUrlFromDocument `
        $document `
        'migration backup' `
        @('version', 'localConfigurations')
    $version = (Get-RequiredJsonProperty $document 'version' 'migration backup').Value
    Assert-JsonInteger $version 'migration backup.version'
    if ($version -ne 1) {
        throw 'Unsupported migration backup version.'
    }
    $localConfigurations = (
        Get-RequiredJsonProperty $document 'localConfigurations' 'migration backup').Value
    Assert-AllowedJsonMembers `
        $localConfigurations `
        @('opencode', 'mimocode') `
        'migration backup.localConfigurations'
    $openCodeContent = (
        Get-RequiredJsonProperty `
            $localConfigurations `
            'opencode' `
            'migration backup.localConfigurations').Value
    $mimocodeContent = (
        Get-RequiredJsonProperty `
            $localConfigurations `
            'mimocode' `
            'migration backup.localConfigurations').Value
    foreach ($entry in @(
            @{ Name = 'opencode'; Value = $openCodeContent },
            @{ Name = 'mimocode'; Value = $mimocodeContent })) {
        if ($null -ne $entry.Value) {
            Assert-JsonString `
                $entry.Value `
                "migration backup.localConfigurations.$($entry.Name)"
            $localDocument = ConvertFrom-StrictJson `
                $entry.Value `
                "migration backup $($entry.Name) content"
            Assert-JsonObject `
                $localDocument `
                "migration backup $($entry.Name) content"
        }
    }
    return [pscustomobject]@{
        Url = $url
        OpenCodeContent = $openCodeContent
        MimocodeContent = $mimocodeContent
    }
}

function Get-ConfigurationOperations(
    [string]$ProjectRoot,
    [string]$Url,
    [bool]$IncludeSource,
    [bool]$DeleteBackup,
    [object]$OpenCodeBaseContent,
    [object]$MimocodeBaseContent) {
    $operations = New-Object System.Collections.ArrayList
    $codexTemplate = Join-Path $ProjectRoot '.codex/config.template.toml'
    $codexOutput = Join-Path $ProjectRoot '.codex/config.toml'
    $openCodeTemplate = Join-Path $ProjectRoot '.opencode/opencode.template.json'
    $openCodeOutput = Join-Path $ProjectRoot '.opencode/opencode.json'
    $mimocodeTemplate = Join-Path $ProjectRoot '.mimocode/mimocode.template.json'
    $mimocodeOutput = Join-Path $ProjectRoot '.mimocode/mimocode.json'
    $sourcePath = Join-Path $ProjectRoot '.agents/mcp.json'
    $backupPath = Join-Path $ProjectRoot '.agents/mcp.local.json'

    $codexContent = Get-CodexRenderedContent $codexTemplate $Url
    $openCodePlan = Get-ProjectClientPlan `
        $openCodeTemplate `
        $openCodeOutput `
        $Url `
        'https://opencode.ai/config.json' `
        $false `
        'OpenCode' `
        $OpenCodeBaseContent
    $mimocodePlan = Get-ProjectClientPlan `
        $mimocodeTemplate `
        $mimocodeOutput `
        $Url `
        'https://mimo.xiaomi.com/mimocode/config.json' `
        $true `
        'MiMoCode' `
        $MimocodeBaseContent

    Add-TextWriteIfChanged $operations $codexOutput $codexContent
    if ($openCodePlan.Changed) {
        [void]$operations.Add((New-WriteOperation $openCodeOutput $openCodePlan.Content))
    }
    if ($mimocodePlan.Changed) {
        [void]$operations.Add((New-WriteOperation $mimocodeOutput $mimocodePlan.Content))
    }
    if ($IncludeSource) {
        Add-TextWriteIfChanged $operations $sourcePath (Get-SourceContent $Url)
    }
    if ($DeleteBackup -and (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
        [void]$operations.Add((New-DeleteOperation $backupPath))
    }
    return @($operations)
}

$projectRoot = Get-ProjectRoot
$sourcePath = Join-Path $projectRoot '.agents/mcp.json'
$backupPath = Join-Path $projectRoot '.agents/mcp.local.json'
$lockPath = Join-Path $projectRoot 'Tools/unity-mcp/ProjectMcpConfig.lock-anchor'
$managedPaths = @(
    $sourcePath,
    $backupPath,
    (Join-Path $projectRoot '.codex/config.toml'),
    (Join-Path $projectRoot '.opencode/opencode.json'),
    (Join-Path $projectRoot '.mimocode/mimocode.json'))
$checkPaths = @(
    $sourcePath,
    (Join-Path $projectRoot '.codex/config.template.toml'),
    (Join-Path $projectRoot '.codex/config.toml'),
    (Join-Path $projectRoot '.opencode/opencode.template.json'),
    (Join-Path $projectRoot '.opencode/opencode.json'),
    (Join-Path $projectRoot '.mimocode/mimocode.template.json'),
    (Join-Path $projectRoot '.mimocode/mimocode.json'))

Invoke-WithConfigLock $lockPath {
    switch ($Operation) {
        'PrepareMigration' {
            $url = Get-NormalizedUnityMcpUrl $InitializeUrl
            $operations = @(
                New-WriteOperation $backupPath (Get-MigrationBackupContent $projectRoot $url)
            )
            $failAfter = Get-FaultInjectionPosition $operations.Count
            Assert-TransactionTargets $operations
            Assert-ManagedPathsAreFilesOrAbsent $managedPaths
            Remove-ToolOwnedResidualFiles $managedPaths
            Invoke-FileTransaction $operations $failAfter
            Write-Host "Saved worktree migration backup: $backupPath"
        }
        'RestoreMigration' {
            $migrationBackup = Read-MigrationBackup $backupPath
            $url = $migrationBackup.Url
            $operations = @(
                Get-ConfigurationOperations `
                    $projectRoot `
                    $url `
                    $true `
                    $true `
                    $migrationBackup.OpenCodeContent `
                    $migrationBackup.MimocodeContent
            )
            $failAfter = Get-FaultInjectionPosition $operations.Count
            Assert-TransactionTargets $operations
            Assert-ManagedPathsAreFilesOrAbsent $managedPaths
            Remove-ToolOwnedResidualFiles $managedPaths
            Invoke-FileTransaction $operations $failAfter
            Write-Host "Restored Unity MCP worktree configuration: $url"
        }
        'Initialize' {
            $url = Get-NormalizedUnityMcpUrl $InitializeUrl
            $operations = @(
                Get-ConfigurationOperations $projectRoot $url $true $false $null $null
            )
            $failAfter = Get-FaultInjectionPosition $operations.Count
            Assert-TransactionTargets $operations
            Assert-ManagedPathsAreFilesOrAbsent $managedPaths
            Remove-ToolOwnedResidualFiles $managedPaths
            Invoke-FileTransaction $operations $failAfter
            Write-Host "Initialized Unity MCP worktree configuration: $url"
        }
        'Sync' {
            $checkFingerprintBefore = if ($Check) {
                Get-FileSetFingerprint $checkPaths
            }
            else {
                $null
            }
            $url = Read-UnityMcpUrl $sourcePath 'worktree-local MCP configuration'
            $operations = @(
                Get-ConfigurationOperations $projectRoot $url $false $false $null $null
            )
            if ($Check) {
                $checkFingerprintAfter = Get-FileSetFingerprint $checkPaths
                if (-not [string]::Equals(
                        $checkFingerprintBefore,
                        $checkFingerprintAfter,
                        [StringComparison]::Ordinal)) {
                    throw 'MCP configuration changed during --check; retry the check.'
                }
                if ($operations.Count -gt 0) {
                    throw 'Generated MCP configuration is stale or missing. Run Sync-ProjectMcpConfig.ps1 without --check.'
                }
                Write-Host "Unity MCP worktree configuration is synchronized: $url"
                break
            }
            $failAfter = Get-FaultInjectionPosition $operations.Count
            Assert-TransactionTargets $operations
            Assert-ManagedPathsAreFilesOrAbsent $managedPaths
            Remove-ToolOwnedResidualFiles $managedPaths
            if ($operations.Count -gt 0) {
                Invoke-FileTransaction $operations $failAfter
                Write-Host "Rendered Unity MCP worktree configuration from ${sourcePath}: $url"
            }
            else {
                Write-Host "Unity MCP worktree configuration is already synchronized: $url"
            }
        }
    }
}
