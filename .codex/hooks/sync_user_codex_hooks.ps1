$ErrorActionPreference = "Stop"

$configPath = Join-Path $env:USERPROFILE ".codex\config.toml"
$markerStart = "# BEGIN tactics unity-auto-compile-guard"
$markerEnd = "# END tactics unity-auto-compile-guard"
$hookBlock = @'
# BEGIN tactics unity-auto-compile-guard
[[hooks.PreToolUse]]
matcher = "Edit|Write|apply_patch|Bash"

[[hooks.PreToolUse.hooks]]
type = "command"
command = "powershell -NoProfile -ExecutionPolicy Bypass -File D:\\codes\\tactics\\.codex\\hooks\\unity_auto_compile_guard_launcher.ps1 pre"
timeout = 30
statusMessage = "检查 C# 改动是否需要编译"

[[hooks.PostToolUse]]
matcher = "Edit|Write|apply_patch|Bash"

[[hooks.PostToolUse.hooks]]
type = "command"
command = "powershell -NoProfile -ExecutionPolicy Bypass -File D:\\codes\\tactics\\.codex\\hooks\\unity_auto_compile_guard_launcher.ps1 post"
timeout = 30
statusMessage = "记录 C# 改动状态"

[[hooks.PostToolUse]]
matcher = "refresh_unity$|mcp__.*__refresh_unity$"

[[hooks.PostToolUse.hooks]]
type = "command"
command = "powershell -NoProfile -ExecutionPolicy Bypass -File D:\\codes\\tactics\\.codex\\hooks\\unity_auto_compile_guard_launcher.ps1 post"
timeout = 30
statusMessage = "检查编译是否已完成"

[[hooks.Stop]]

[[hooks.Stop.hooks]]
type = "command"
command = "powershell -NoProfile -ExecutionPolicy Bypass -File D:\\codes\\tactics\\.codex\\hooks\\unity_auto_compile_guard_launcher.ps1 stop"
timeout = 30
statusMessage = "检查是否遗漏 Unity 编译"
# END tactics unity-auto-compile-guard
'@

if (Test-Path -LiteralPath $configPath) {
    $text = [System.IO.File]::ReadAllText($configPath, [System.Text.Encoding]::UTF8)
} else {
    $text = ""
}

if ($text.Contains($markerStart) -and $text.Contains($markerEnd)) {
    $pattern = [regex]::Escape($markerStart) + ".*?" + [regex]::Escape($markerEnd)
    $text = [regex]::Replace(
        $text,
        $pattern,
        $hookBlock.TrimEnd(),
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
} else {
    $trimmed = $text.TrimEnd("`r", "`n")
    if ($trimmed.Length -gt 0) {
        $text = $trimmed + "`r`n`r`n" + $hookBlock.TrimEnd() + "`r`n"
    } else {
        $text = $hookBlock.TrimEnd() + "`r`n"
    }
}

if ($text -match '(?ms)^\[features\]\r?\n') {
    $featuresPattern = '(?ms)^\[features\]\r?\n(?<body>.*?)(?=^\[|^\[\[|\z)'
    $match = [regex]::Match($text, $featuresPattern)
    if ($match.Success) {
        $body = $match.Groups["body"].Value
        if ($body -notmatch '(?m)^hooks\s*=\s*true\s*$') {
            $newBody = "hooks = true`r`n" + $body
            $text = $text.Substring(0, $match.Groups["body"].Index) + $newBody + $text.Substring($match.Groups["body"].Index + $match.Groups["body"].Length)
        }
    }
} else {
    $text = "[features]`r`nhooks = true`r`n`r`n" + $text.TrimStart("`r", "`n")
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($configPath, $text, $utf8NoBom)

Write-Output "Synced hook block into $configPath"
