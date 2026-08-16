[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [Parameter(Mandatory = $true)]
    [string]$DiagnosticsDirectory,
    [ValidateSet('gl_compatibility', 'default')]
    [string]$Renderer = 'gl_compatibility',
    [ValidateRange(5, 120)]
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
$exe = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Exported executable not found: $exe" }
$diagnostics = [IO.Path]::GetFullPath($DiagnosticsDirectory)
New-Item -ItemType Directory -Path $diagnostics -Force | Out-Null
$attempt = "$Renderer-$([Guid]::NewGuid().ToString('N'))"
$userRoot = Join-Path $diagnostics "user-$attempt"
$stdout = Join-Path $diagnostics "$attempt.stdout.log"
$stderr = Join-Path $diagnostics "$attempt.stderr.log"
New-Item -ItemType Directory -Path $userRoot | Out-Null

$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
try {
    $env:APPDATA = Join-Path $userRoot 'Roaming'
    $env:LOCALAPPDATA = Join-Path $userRoot 'Local'
    New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA -Force | Out-Null
    $arguments = @('--headless', '--quit-after', '120')
    if ($Renderer -ne 'default') { $arguments += @('--rendering-method', $Renderer) }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        if (-not $process.HasExited) { $process.Kill($true) }
        throw "Exported executable did not exit within $TimeoutSeconds seconds ($Renderer)."
    }
    if ($process.ExitCode -ne 0) { throw "Exported executable exited with code $($process.ExitCode) ($Renderer)." }
    $combined = ((Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue) + "`n" +
        (Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue))
    $fatalPatterns = '(?i)(missing assembly|cannot open.*\.pck|failed to load|uid.*not found|duplicate type|unhandled exception|disposed object)'
    if ($combined -match $fatalPatterns) { throw "Exported executable log contains a fatal signature ($Renderer)." }
    $readyMarker = 'Tactics Godot playable run UI ready'
    if ($combined -notmatch [Regex]::Escape($readyMarker)) {
        throw "Exported executable did not emit the startup ready marker ($Renderer): $readyMarker"
    }
    [pscustomobject]@{ Renderer = $Renderer; ExitCode = 0; UserDataRoot = $userRoot; Stdout = $stdout; Stderr = $stderr }
}
finally {
    $env:APPDATA = $oldAppData
    $env:LOCALAPPDATA = $oldLocalAppData
}
