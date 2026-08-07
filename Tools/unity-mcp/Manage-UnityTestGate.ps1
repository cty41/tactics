#requires -Version 5.1
[CmdletBinding(DefaultParameterSetName = 'Create')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Create')]
    [switch]$Create,
    [Parameter(Mandatory, ParameterSetName = 'Create')]
    [ValidateSet('Targeted', 'Full', 'Explicit')]
    [string]$Scope,
    [Parameter(ParameterSetName = 'Create')]
    [string[]]$EditModeTestName,
    [Parameter(ParameterSetName = 'Create')]
    [string[]]$PlayModeTestName,

    [Parameter(Mandatory, ParameterSetName = 'Next')]
    [switch]$Next,

    [Parameter(Mandatory, ParameterSetName = 'RecordStart')]
    [switch]$RecordStart,
    [Parameter(Mandatory, ParameterSetName = 'RecordStart')]
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [Parameter(ParameterSetName = 'Next')]
    [Parameter(Mandatory, ParameterSetName = 'CancelReservation')]
    [ValidatePattern('^[a-z0-9-]+$')]
    [string]$JobKey,
    [Parameter(Mandatory, ParameterSetName = 'RecordStart')]
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$JobId,
    [Parameter(ParameterSetName = 'Next')]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$SupersedesJobId,
    [Parameter(ParameterSetName = 'Next')]
    [string]$SupersedeReason,
    [Parameter(Mandatory, ParameterSetName = 'RecordStart')]
    [Parameter(Mandatory, ParameterSetName = 'CancelReservation')]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$ReservationId,

    [Parameter(Mandatory, ParameterSetName = 'CancelReservation')]
    [switch]$CancelReservation,
    [Parameter(Mandatory, ParameterSetName = 'CancelReservation')]
    [ValidateNotNullOrEmpty()]
    [string]$CancellationReason,

    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [switch]$RecordResult,
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidateSet('succeeded', 'failed')]
    [string]$Status,
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Total,
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Passed,
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Failed,
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Skipped,
    [Parameter(ParameterSetName = 'RecordResult')]
    [ValidateRange(0, [double]::MaxValue)]
    [double]$DurationSeconds = 0,

    [Parameter(Mandatory, ParameterSetName = 'Validate')]
    [switch]$Validate,

    [Parameter(ParameterSetName = 'Create')]
    [Parameter(Mandatory, ParameterSetName = 'Next')]
    [Parameter(Mandatory, ParameterSetName = 'RecordStart')]
    [Parameter(Mandatory, ParameterSetName = 'RecordResult')]
    [Parameter(Mandatory, ParameterSetName = 'Validate')]
    [Parameter(Mandatory, ParameterSetName = 'CancelReservation')]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$GateId,

    [Parameter(ParameterSetName = 'Create')]
    [Parameter(ParameterSetName = 'Next')]
    [Parameter(ParameterSetName = 'RecordStart')]
    [Parameter(ParameterSetName = 'RecordResult')]
    [Parameter(ParameterSetName = 'Validate')]
    [Parameter(ParameterSetName = 'CancelReservation')]
    [string]$StateRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}

function Get-StateRoot([string]$RequestedRoot) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return [System.IO.Path]::GetFullPath($RequestedRoot)
    }

    return Join-Path (Get-ProjectRoot) 'Library/MCPForUnity/TestGates'
}

function Get-GatePath([string]$Root, [string]$Id) {
    return Join-Path $Root "$Id.json"
}

function Invoke-WithGateLock([string]$Path, [scriptblock]$Action) {
    $directory = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $lockStream = $null
    try {
        while ($null -eq $lockStream) {
            try {
                $lockStream = [System.IO.File]::Open(
                    $Path,
                    [System.IO.FileMode]::OpenOrCreate,
                    [System.IO.FileAccess]::ReadWrite,
                    [System.IO.FileShare]::None)
            }
            catch [System.IO.IOException] {
                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "Timed out waiting for Unity test gate lock: $Path"
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

function Assert-RequiredProperties([object]$Object, [string[]]$Required, [string]$Context) {
    if ($null -eq $Object) {
        throw "Missing Unity test gate object: $Context"
    }
    $propertyNames = @($Object.PSObject.Properties.Name)
    $missing = @($Required | Where-Object { $_ -notin $propertyNames })
    if ($missing.Count -gt 0) {
        throw "Unity test gate $Context is missing field(s): $($missing -join ', ')"
    }
}

function Assert-JobStateConsistency([object]$Job) {
    Assert-RequiredProperties $Job @(
        'key', 'mode', 'kind', 'testNames', 'payload', 'status',
        'reservationId', 'reservedAtUtc',
        'pendingSupersedesJobId', 'pendingSupersedeReason', 'attempts',
        'cancellations', 'result'
    ) "job $($Job.key)"

    $attempts = @($Job.attempts)
    $reservationIds = @{}
    for ($attemptIndex = 0; $attemptIndex -lt $attempts.Count; $attemptIndex++) {
        $attempt = $attempts[$attemptIndex]
        Assert-RequiredProperties $attempt @(
            'jobId', 'status', 'startedAtUtc', 'completedAtUtc',
            'reservationId', 'legacyReservationUnknown',
            'supersedesJobId', 'supersedeReason'
        ) "attempt in $($Job.key)"
        if ($attempt.jobId -notmatch '^[0-9a-f]{32}$' -or
            ($null -ne $attempt.reservationId -and
                $attempt.reservationId -notmatch '^[0-9a-f]{32}$') -or
            $attempt.status -notin @('running', 'succeeded', 'failed')) {
            throw "Unity test gate contains an invalid attempt for $($Job.key)."
        }
        if ($attempt.status -eq 'running' -and
            -not [string]::IsNullOrWhiteSpace([string]$attempt.completedAtUtc)) {
            throw "Running Unity test attempt has a completion time: $($Job.key)"
        }
        if ($attempt.status -ne 'running' -and
            [string]::IsNullOrWhiteSpace([string]$attempt.completedAtUtc)) {
            throw "Terminal Unity test attempt is missing a completion time: $($Job.key)"
        }
        if ($attemptIndex -eq 0) {
            if (-not [string]::IsNullOrWhiteSpace([string]$attempt.supersedesJobId) -or
                -not [string]::IsNullOrWhiteSpace([string]$attempt.supersedeReason)) {
                throw "First Unity test attempt cannot supersede another job: $($Job.key)"
            }
        }
        else {
            $previousAttempt = $attempts[$attemptIndex - 1]
            if ($previousAttempt.status -ne 'failed' -or
                $attempt.supersedesJobId -ne $previousAttempt.jobId -or
                [string]::IsNullOrWhiteSpace([string]$attempt.supersedeReason)) {
                throw "Unity test retry does not supersede the immediately preceding failure: $($Job.key)"
            }
        }
        if ($attemptIndex + 1 -lt $attempts.Count -and $attempt.status -ne 'failed') {
            throw "Only a failed Unity test attempt may precede another attempt: $($Job.key)"
        }
        if ($null -ne $attempt.reservationId) {
            if ($reservationIds.ContainsKey($attempt.reservationId)) {
                throw "Unity test gate reuses a reservation ID: $($attempt.reservationId)"
            }
            $reservationIds[$attempt.reservationId] = $true
        }
    }

    foreach ($cancellation in @($Job.cancellations)) {
        Assert-RequiredProperties $cancellation @(
            'reservationId', 'cancelledAtUtc', 'reason', 'priorStatus'
        ) "cancellation in $($Job.key)"
        if ($cancellation.reservationId -notmatch '^[0-9a-f]{32}$' -or
            [string]::IsNullOrWhiteSpace([string]$cancellation.cancelledAtUtc) -or
            [string]::IsNullOrWhiteSpace([string]$cancellation.reason) -or
            $cancellation.priorStatus -ne 'reserved') {
            throw "Unity test gate contains invalid cancellation evidence for $($Job.key)."
        }
        if ($reservationIds.ContainsKey($cancellation.reservationId)) {
            throw "Unity test gate reuses a reservation ID: $($cancellation.reservationId)"
        }
        $reservationIds[$cancellation.reservationId] = $true
    }

    switch ($Job.status) {
        'planned' {
            if ($attempts.Count -ne 0 -or $null -ne $Job.result -or
                -not [string]::IsNullOrWhiteSpace([string]$Job.reservationId)) {
                throw "Planned Unity test job has inconsistent state: $($Job.key)"
            }
        }
        'reserved' {
            if ([string]::IsNullOrWhiteSpace([string]$Job.reservationId) -or
                [string]::IsNullOrWhiteSpace([string]$Job.reservedAtUtc)) {
                throw "Reserved Unity test job is missing reservation evidence: $($Job.key)"
            }
            if ($reservationIds.ContainsKey($Job.reservationId)) {
                throw "Active reservation reuses historical evidence: $($Job.reservationId)"
            }
            if ($attempts.Count -eq 0) {
                if ($null -ne $Job.result -or
                    -not [string]::IsNullOrWhiteSpace([string]$Job.pendingSupersedesJobId) -or
                    -not [string]::IsNullOrWhiteSpace([string]$Job.pendingSupersedeReason)) {
                    throw "Initial reservation contains retry evidence: $($Job.key)"
                }
            }
            elseif ($attempts[-1].status -ne 'failed' -or
                $Job.pendingSupersedesJobId -ne $attempts[-1].jobId -or
                [string]::IsNullOrWhiteSpace([string]$Job.pendingSupersedeReason) -or
                $null -eq $Job.result) {
                throw "Retry reservation is not linked to the last failed attempt: $($Job.key)"
            }
        }
        'running' {
            if ($attempts.Count -eq 0 -or $attempts[-1].status -ne 'running' -or
                $null -ne $Job.result -or
                -not [string]::IsNullOrWhiteSpace([string]$Job.reservationId)) {
                throw "Running Unity test job has inconsistent state: $($Job.key)"
            }
        }
        { $_ -in @('succeeded', 'failed') } {
            if ($attempts.Count -eq 0 -or $attempts[-1].status -ne $Job.status -or
                $null -eq $Job.result) {
                throw "Terminal Unity test job is missing matching evidence: $($Job.key)"
            }
            Assert-RequiredProperties $Job.result @(
                'jobId', 'total', 'passed', 'failed', 'skipped', 'durationSeconds'
            ) "result for $($Job.key)"
            if ($Job.result.jobId -ne $attempts[-1].jobId -or
                $Job.result.total -lt 0 -or $Job.result.passed -lt 0 -or
                $Job.result.failed -lt 0 -or $Job.result.skipped -lt 0 -or
                $Job.result.passed + $Job.result.failed + $Job.result.skipped -ne $Job.result.total) {
                throw "Terminal Unity test job has invalid result counts: $($Job.key)"
            }
            if ($Job.status -eq 'succeeded' -and
                ($Job.result.total -eq 0 -or $Job.result.failed -ne 0)) {
                throw "Succeeded Unity test job has invalid result evidence: $($Job.key)"
            }
        }
    }
}

function Test-OrdinalStringArrayEqual([object[]]$Actual, [object[]]$Expected) {
    $actualValues = @($Actual | ForEach-Object { [string]$_ })
    $expectedValues = @($Expected | ForEach-Object { [string]$_ })
    if ($actualValues.Count -ne $expectedValues.Count) {
        return $false
    }
    for ($index = 0; $index -lt $actualValues.Count; $index++) {
        if (-not [string]::Equals(
                $actualValues[$index],
                $expectedValues[$index],
                [StringComparison]::Ordinal)) {
            return $false
        }
    }
    return $true
}

function Assert-CanonicalJobPlan([object]$State) {
    $jobs = @($State.jobs)
    if ($jobs.Count -eq 0) {
        throw 'Unity test gate must contain at least one canonical job.'
    }
    foreach ($job in $jobs) {
        $allowedPayloadProperties = @(
            'mode', 'init_timeout', 'include_failed_tests', 'include_details'
        )
        if ($job.kind -ne 'full') {
            $allowedPayloadProperties += 'test_names'
        }
        Assert-RequiredProperties $job.payload @(
            'mode', 'init_timeout', 'include_failed_tests', 'include_details'
        ) "payload for $($job.key)"
        $unexpectedPayloadProperties = @(
            $job.payload.PSObject.Properties.Name |
                Where-Object { $_ -notin $allowedPayloadProperties }
        )
        if ($unexpectedPayloadProperties.Count -gt 0) {
            throw "Unity test gate payload contains unsupported fields: $($job.key)"
        }
        if ($job.payload.mode -ne $job.mode -or
            $job.payload.init_timeout -ne 120000 -or
            $job.payload.include_failed_tests -ne $true -or
            $job.payload.include_details -ne $false) {
            throw "Unity test gate has a non-canonical payload: $($job.key)"
        }
        $testNames = @($job.testNames)
        if ($job.kind -eq 'full') {
            if ($testNames.Count -ne 0 -or
                'test_names' -in @($job.payload.PSObject.Properties.Name)) {
                throw "Full Unity test job cannot contain filters: $($job.key)"
            }
        }
        else {
            if ($testNames.Count -eq 0 -or
                'test_names' -notin @($job.payload.PSObject.Properties.Name) -or
                -not (Test-OrdinalStringArrayEqual $testNames @($job.payload.test_names))) {
                throw "Filtered Unity test job has inconsistent test names: $($job.key)"
            }
            $normalized = @(Get-NormalizedTestNames $testNames)
            if (-not (Test-OrdinalStringArrayEqual $testNames $normalized)) {
                throw "Filtered Unity test names are not canonical: $($job.key)"
            }
        }
    }

    switch ($State.scope) {
        'Full' {
            if ($jobs.Count -ne 2 -or
                $jobs[0].key -ne 'editmode-full' -or $jobs[0].mode -ne 'EditMode' -or $jobs[0].kind -ne 'full' -or
                $jobs[1].key -ne 'playmode-full' -or $jobs[1].mode -ne 'PlayMode' -or $jobs[1].kind -ne 'full') {
                throw 'Full Unity test gate must contain canonical EditMode and PlayMode jobs.'
            }
        }
        'Targeted' {
            if ($jobs.Count -gt 2) {
                throw 'Targeted Unity test gate contains too many jobs.'
            }
            foreach ($job in $jobs) {
                $expectedKey = if ($job.mode -eq 'EditMode') { 'editmode-targeted' } else { 'playmode-targeted' }
                if ($job.kind -ne 'targeted' -or $job.key -ne $expectedKey) {
                    throw "Targeted Unity test gate contains a non-canonical job: $($job.key)"
                }
            }
        }
        'Explicit' {
            $modeCounters = @{ EditMode = 0; PlayMode = 0 }
            foreach ($job in $jobs) {
                $modeCounters[$job.mode]++
                $prefix = if ($job.mode -eq 'EditMode') { 'editmode' } else { 'playmode' }
                $expectedKey = "$prefix-explicit-{0:d3}" -f $modeCounters[$job.mode]
                if ($job.kind -ne 'explicit' -or @($job.testNames).Count -ne 1 -or
                    $job.key -ne $expectedKey) {
                    throw "Explicit Unity test gate contains a non-canonical job: $($job.key)"
                }
            }
        }
    }
}

function Assert-GateState([object]$State, [string]$ExpectedGateId) {
    Assert-RequiredProperties $State @(
        'schemaVersion', 'gateId', 'scope', 'createdAtUtc', 'planHash', 'jobs'
    ) 'state'
    if ($null -eq $State -or $State.schemaVersion -ne 3 -or $State.gateId -ne $ExpectedGateId) {
        throw "Invalid Unity test gate state for $ExpectedGateId."
    }

    if ($State.scope -notin @('Targeted', 'Full', 'Explicit')) {
        throw "Invalid Unity test gate scope: $($State.scope)"
    }
    if ([string]::IsNullOrWhiteSpace([string]$State.createdAtUtc) -or
        $State.planHash -ne (Get-PlanFingerprint @($State.jobs))) {
        throw "Unity test gate plan fingerprint is invalid: $ExpectedGateId"
    }

    Assert-CanonicalJobPlan $State

    $jobKeys = @{}
    $jobIds = @{}
    foreach ($job in @($State.jobs)) {
        if ([string]::IsNullOrWhiteSpace($job.key) -or $jobKeys.ContainsKey($job.key)) {
            throw "Unity test gate contains a missing or duplicate job key."
        }
        $jobKeys[$job.key] = $true

        if ($job.mode -notin @('EditMode', 'PlayMode') -or
            $job.status -notin @('planned', 'reserved', 'running', 'succeeded', 'failed')) {
            throw "Unity test gate contains an invalid job: $($job.key)"
        }
        Assert-JobStateConsistency $job
        foreach ($attempt in @($job.attempts)) {
            if ($jobIds.ContainsKey($attempt.jobId)) {
                throw "Unity test gate reuses an MCP job ID: $($attempt.jobId)"
            }
            $jobIds[$attempt.jobId] = $true
        }
    }
}

function ConvertTo-CurrentGateState([object]$State) {
    if ($null -eq $State -or $State.schemaVersion -notin @(1, 2, 3)) {
        return $State
    }
    if ($State.schemaVersion -eq 3) {
        return $State
    }
    foreach ($job in @($State.jobs)) {
        if ('cancellations' -notin @($job.PSObject.Properties.Name)) {
            $job | Add-Member -MemberType NoteProperty -Name 'cancellations' -Value @()
        }
        foreach ($attempt in @($job.attempts)) {
            if ('reservationId' -notin @($attempt.PSObject.Properties.Name)) {
                $attempt | Add-Member -MemberType NoteProperty -Name 'reservationId' -Value $null
            }
            if ('legacyReservationUnknown' -notin @($attempt.PSObject.Properties.Name)) {
                $isLegacyUnknown = $null -eq $attempt.reservationId
                $attempt | Add-Member -MemberType NoteProperty `
                    -Name 'legacyReservationUnknown' `
                    -Value $isLegacyUnknown
            }
        }
        if ($null -ne $job.result -and
            'jobId' -notin @($job.result.PSObject.Properties.Name)) {
            $attempts = @($job.attempts)
            $legacyJobId = if ($attempts.Count -gt 0) { $attempts[-1].jobId } else { $null }
            $job.result | Add-Member -MemberType NoteProperty -Name 'jobId' -Value $legacyJobId
        }
    }
    if ('planHash' -notin @($State.PSObject.Properties.Name)) {
        $State | Add-Member -MemberType NoteProperty `
            -Name 'planHash' `
            -Value (Get-PlanFingerprint @($State.jobs))
    }
    $State.schemaVersion = 3
    return $State
}

function Read-GateState([string]$Path, [string]$ExpectedGateId) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Unity test gate does not exist: $Path"
    }

    $state = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $state = ConvertTo-CurrentGateState $state
    Assert-GateState $state $ExpectedGateId
    return $state
}

function Write-GateState([string]$Path, [object]$State) {
    $directory = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporaryPath = "$Path.$PID.$([Guid]::NewGuid().ToString('N')).tmp"
    $backupPath = "$Path.$PID.$([Guid]::NewGuid().ToString('N')).bak"
    $encoding = New-Object System.Text.UTF8Encoding($false)
    $committed = $false
    try {
        $json = $State | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($temporaryPath, $json, $encoding)
        $validation = Get-Content -LiteralPath $temporaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-GateState $validation $State.gateId

        if (Test-Path -LiteralPath $Path) {
            [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
        }
        else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
        $committed = $true
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
        if ($committed -and (Test-Path -LiteralPath $backupPath)) {
            # The replacement is already committed. Cleanup cannot retroactively turn a
            # successful reservation/result transition into a reported failure.
            Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-NormalizedTestNames([string[]]$Names) {
    $unique = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($name in @($Names)) {
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            [void]$unique.Add($name.Trim())
        }
    }
    $result = @($unique)
    [Array]::Sort($result, [StringComparer]::Ordinal)
    return $result
}

function Get-PlanFingerprint([object[]]$Jobs) {
    $lines = @(
        $Jobs | ForEach-Object {
            $names = @($_.testNames | ForEach-Object { [string]$_ }) -join ','
            "$($_.key)|$($_.mode)|$($_.kind)|$names"
        }
    )
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
        return -join ($sha256.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $sha256.Dispose()
    }
}

function New-TestJob([string]$Key, [string]$Mode, [string]$Kind, [string[]]$TestNames) {
    $payload = [ordered]@{
        mode = $Mode
        init_timeout = 120000
        include_failed_tests = $true
        include_details = $false
    }
    if ($Kind -ne 'full') {
        $payload.test_names = @($TestNames)
    }

    return [ordered]@{
        key = $Key
        mode = $Mode
        kind = $Kind
        testNames = @($TestNames)
        payload = $payload
        status = 'planned'
        reservationId = $null
        reservedAtUtc = $null
        pendingSupersedesJobId = $null
        pendingSupersedeReason = $null
        attempts = @()
        cancellations = @()
        result = $null
    }
}

function New-GateState([string]$Id, [string]$GateScope) {
    $editModeNames = @(Get-NormalizedTestNames $EditModeTestName)
    $playModeNames = @(Get-NormalizedTestNames $PlayModeTestName)
    $jobs = @()

    switch ($GateScope) {
        'Full' {
            if ($editModeNames.Count -gt 0 -or $playModeNames.Count -gt 0) {
                throw 'Full gate does not accept test name filters.'
            }
            $jobs += New-TestJob 'editmode-full' 'EditMode' 'full' @()
            $jobs += New-TestJob 'playmode-full' 'PlayMode' 'full' @()
        }
        'Targeted' {
            if ($editModeNames.Count -gt 0) {
                $jobs += New-TestJob 'editmode-targeted' 'EditMode' 'targeted' $editModeNames
            }
            if ($playModeNames.Count -gt 0) {
                $jobs += New-TestJob 'playmode-targeted' 'PlayMode' 'targeted' $playModeNames
            }
            if ($jobs.Count -eq 0) {
                throw 'Targeted gate requires at least one EditMode or PlayMode test name.'
            }
        }
        'Explicit' {
            $index = 0
            foreach ($name in $editModeNames) {
                $index++
                $jobs += New-TestJob ("editmode-explicit-{0:d3}" -f $index) 'EditMode' 'explicit' @($name)
            }
            $index = 0
            foreach ($name in $playModeNames) {
                $index++
                $jobs += New-TestJob ("playmode-explicit-{0:d3}" -f $index) 'PlayMode' 'explicit' @($name)
            }
            if ($jobs.Count -eq 0) {
                throw 'Explicit gate requires at least one exact test name.'
            }
        }
    }

    $state = [ordered]@{
        schemaVersion = 3
        gateId = $Id
        scope = $GateScope
        createdAtUtc = [DateTime]::UtcNow.ToString('o')
        planHash = Get-PlanFingerprint $jobs
        jobs = $jobs
    }
    return $state
}

function Get-GateJob([object]$State, [string]$Key) {
    $matches = @($State.jobs | Where-Object { $_.key -eq $Key })
    if ($matches.Count -ne 1) {
        throw "Unknown Unity test gate job key: $Key"
    }
    return $matches[0]
}

function Get-ActiveGateJob([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root)) {
        return $null
    }

    foreach ($file in Get-ChildItem -LiteralPath $Root -Filter '*.json' -File) {
        $state = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $state = ConvertTo-CurrentGateState $state
        Assert-GateState $state ([System.IO.Path]::GetFileNameWithoutExtension($file.Name))
        $activeJobs = @($state.jobs | Where-Object { $_.status -in @('reserved', 'running') })
        if ($activeJobs.Count -gt 0) {
            return [ordered]@{ gateId = $state.gateId; job = $activeJobs[0] }
        }
    }

    return $null
}

function Get-JobIdBinding([string]$Root, [string]$McpJobId) {
    if (-not (Test-Path -LiteralPath $Root)) {
        return $null
    }
    foreach ($file in Get-ChildItem -LiteralPath $Root -Filter '*.json' -File) {
        $state = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $state = ConvertTo-CurrentGateState $state
        Assert-GateState $state ([System.IO.Path]::GetFileNameWithoutExtension($file.Name))
        foreach ($job in @($state.jobs)) {
            foreach ($attempt in @($job.attempts)) {
                if ($attempt.jobId -eq $McpJobId) {
                    return [ordered]@{ gateId = $state.gateId; jobKey = $job.key }
                }
            }
        }
    }
    return $null
}

function Assert-StateRootUniqueEvidence([string]$Root) {
    $jobIds = @{}
    $reservationIds = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Filter '*.json' -File) {
        $state = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $state = ConvertTo-CurrentGateState $state
        Assert-GateState $state ([System.IO.Path]::GetFileNameWithoutExtension($file.Name))
        foreach ($job in @($state.jobs)) {
            foreach ($attempt in @($job.attempts)) {
                if ($jobIds.ContainsKey($attempt.jobId)) {
                    throw "MCP job ID is reused across Unity test gates: $($attempt.jobId)"
                }
                $jobIds[$attempt.jobId] = $true
                if ($null -ne $attempt.reservationId) {
                    if ($reservationIds.ContainsKey($attempt.reservationId)) {
                        throw "Reservation ID is reused across Unity test gates: $($attempt.reservationId)"
                    }
                    $reservationIds[$attempt.reservationId] = $true
                }
            }
            foreach ($cancellation in @($job.cancellations)) {
                if ($reservationIds.ContainsKey($cancellation.reservationId)) {
                    throw "Reservation ID is reused across Unity test gates: $($cancellation.reservationId)"
                }
                $reservationIds[$cancellation.reservationId] = $true
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$job.reservationId)) {
                if ($reservationIds.ContainsKey($job.reservationId)) {
                    throw "Reservation ID is reused across Unity test gates: $($job.reservationId)"
                }
                $reservationIds[$job.reservationId] = $true
            }
        }
    }
}

function Reserve-GateJob(
    [object]$Job,
    [string]$SupersededJobId,
    [string]$Reason) {
    $reservation = [Guid]::NewGuid().ToString('N')
    $Job.status = 'reserved'
    $Job.reservationId = $reservation
    $Job.reservedAtUtc = [DateTime]::UtcNow.ToString('o')
    $Job.pendingSupersedesJobId = if ([string]::IsNullOrWhiteSpace($SupersededJobId)) {
        $null
    } else {
        $SupersededJobId
    }
    $Job.pendingSupersedeReason = if ([string]::IsNullOrWhiteSpace($Reason)) {
        $null
    } else {
        $Reason.Trim()
    }
    return $reservation
}

function Write-JsonOutput([object]$Value) {
    $Value | ConvertTo-Json -Depth 20 -Compress
}

if ($PSCmdlet.ParameterSetName -eq 'Next') {
    $hasJobKey = $PSBoundParameters.ContainsKey('JobKey')
    $hasSupersedesJobId = $PSBoundParameters.ContainsKey('SupersedesJobId')
    $hasSupersedeReason = $PSBoundParameters.ContainsKey('SupersedeReason')
    if (($hasSupersedesJobId -or $hasSupersedeReason) -and -not $hasJobKey) {
        throw 'Retry evidence requires JobKey.'
    }
    if ($hasJobKey -and (-not $hasSupersedesJobId -or -not $hasSupersedeReason)) {
        throw 'Retry reservation requires JobKey, SupersedesJobId, and SupersedeReason together.'
    }
}

$resolvedStateRoot = Get-StateRoot $StateRoot
if ($PSCmdlet.ParameterSetName -eq 'Create' -and [string]::IsNullOrWhiteSpace($GateId)) {
    $GateId = [Guid]::NewGuid().ToString('N')
}
$gatePath = Get-GatePath $resolvedStateRoot $GateId
$globalLockPath = Join-Path $resolvedStateRoot '.unity-test-gate.lock'

Invoke-WithGateLock $globalLockPath {
    switch ($PSCmdlet.ParameterSetName) {
        'Create' {
            if (Test-Path -LiteralPath $gatePath) {
                throw "Unity test gate already exists: $GateId"
            }
            $state = New-GateState $GateId $Scope
            Write-GateState $gatePath $state
            Write-JsonOutput $state
        }
        'Next' {
            $state = Read-GateState $gatePath $GateId
            $active = Get-ActiveGateJob $resolvedStateRoot
            if ($null -ne $active) {
                $activeState = if ($active.gateId -eq $GateId) { 'waiting' } else { 'blocked' }
                Write-JsonOutput ([ordered]@{
                    gateId = $GateId
                    state = $activeState
                    activeGateId = $active.gateId
                    job = $active.job
                })
                break
            }

            $failedJobs = @($state.jobs | Where-Object { $_.status -eq 'failed' })
            $planned = @($state.jobs | Where-Object { $_.status -eq 'planned' })
            if (-not [string]::IsNullOrWhiteSpace($JobKey)) {
                $job = Get-GateJob $state $JobKey
                $attempts = @($job.attempts)
                $lastAttempt = if ($attempts.Count -gt 0) { $attempts[-1] } else { $null }
                if ($job.status -ne 'failed' -or
                    $null -eq $lastAttempt -or
                    $lastAttempt.jobId -ne $SupersedesJobId -or
                    [string]::IsNullOrWhiteSpace($SupersedeReason)) {
                    throw "Retry reservation for $JobKey requires the failed job id and a reason."
                }
                $reservation = Reserve-GateJob $job $SupersedesJobId $SupersedeReason
                Write-GateState $gatePath $state
                Write-JsonOutput ([ordered]@{
                    gateId = $GateId
                    state = 'ready'
                    reservationId = $reservation
                    job = $job
                })
            }
            elseif ($failedJobs.Count -gt 0) {
                Write-JsonOutput ([ordered]@{ gateId = $GateId; state = 'failed'; job = $failedJobs[0] })
            }
            elseif ($planned.Count -gt 0) {
                $job = $planned[0]
                $reservation = Reserve-GateJob $job $null $null
                Write-GateState $gatePath $state
                Write-JsonOutput ([ordered]@{
                    gateId = $GateId
                    state = 'ready'
                    reservationId = $reservation
                    job = $job
                })
            }
            else {
                Write-JsonOutput ([ordered]@{ gateId = $GateId; state = 'complete'; job = $null })
            }
        }
        'RecordStart' {
            $state = Read-GateState $gatePath $GateId
            $job = Get-GateJob $state $JobKey
            $attempts = @($job.attempts)
            $lastAttempt = if ($attempts.Count -gt 0) { $attempts[-1] } else { $null }

            if ($null -ne $lastAttempt -and $lastAttempt.status -eq 'running') {
                if ($lastAttempt.jobId -ne $JobId -or
                    (-not $lastAttempt.legacyReservationUnknown -and
                        $lastAttempt.reservationId -ne $ReservationId)) {
                    throw "Job $JobKey already has a running MCP job: $($lastAttempt.jobId)"
                }
                if ($lastAttempt.legacyReservationUnknown) {
                    $lastAttempt.reservationId = $ReservationId
                    $lastAttempt.legacyReservationUnknown = $false
                    Write-GateState $gatePath $state
                }
                Write-JsonOutput $state
                break
            }
            $existingBinding = Get-JobIdBinding $resolvedStateRoot $JobId
            if ($null -ne $existingBinding) {
                throw "MCP job $JobId is already bound to gate $($existingBinding.gateId) job $($existingBinding.jobKey)."
            }
            if ($job.status -ne 'reserved' -or $job.reservationId -ne $ReservationId) {
                throw "Job $JobKey must hold the matching reservation before recording an MCP job."
            }

            $attempt = [ordered]@{
                jobId = $JobId
                status = 'running'
                startedAtUtc = [DateTime]::UtcNow.ToString('o')
                completedAtUtc = $null
                reservationId = $ReservationId
                legacyReservationUnknown = $false
                supersedesJobId = $job.pendingSupersedesJobId
                supersedeReason = $job.pendingSupersedeReason
            }
            $job.attempts = @($attempts + $attempt)
            $job.status = 'running'
            $job.reservationId = $null
            $job.reservedAtUtc = $null
            $job.pendingSupersedesJobId = $null
            $job.pendingSupersedeReason = $null
            $job.result = $null
            Write-GateState $gatePath $state
            Write-JsonOutput $state
        }
        'RecordResult' {
            if ($Passed + $Failed + $Skipped -ne $Total) {
                throw 'Passed + Failed + Skipped must equal Total.'
            }
            if ($Status -eq 'succeeded' -and ($Failed -gt 0 -or $Total -eq 0)) {
                throw 'A succeeded job requires Total > 0 and Failed = 0.'
            }

            $state = Read-GateState $gatePath $GateId
            $job = Get-GateJob $state $JobKey
            $attempts = @($job.attempts)
            if ($attempts.Count -eq 0) {
                throw "Job $JobKey has no recorded MCP job."
            }
            $attempt = $attempts[-1]
            $result = [ordered]@{
                jobId = $JobId
                total = $Total
                passed = $Passed
                failed = $Failed
                skipped = $Skipped
                durationSeconds = $DurationSeconds
            }
            if ($attempt.jobId -eq $JobId -and $attempt.status -eq $Status -and
                $job.result.total -eq $Total -and
                $job.result.passed -eq $Passed -and
                $job.result.failed -eq $Failed -and
                $job.result.skipped -eq $Skipped -and
                $job.result.durationSeconds -eq $DurationSeconds) {
                Write-JsonOutput $state
                break
            }
            if ($attempt.jobId -ne $JobId -or $attempt.status -ne 'running') {
                throw "Job result does not match the running MCP job for $JobKey."
            }

            $attempt.status = $Status
            $attempt.completedAtUtc = [DateTime]::UtcNow.ToString('o')
            $job.status = $Status
            $job.result = $result
            Write-GateState $gatePath $state
            Write-JsonOutput $state
        }
        'CancelReservation' {
            $state = Read-GateState $gatePath $GateId
            $job = Get-GateJob $state $JobKey
            if ([string]::IsNullOrWhiteSpace($CancellationReason)) {
                throw 'CancellationReason must contain non-whitespace evidence.'
            }
            $trimmedCancellationReason = $CancellationReason.Trim()
            $existingCancellation = @(
                $job.cancellations |
                    Where-Object { $_.reservationId -eq $ReservationId }
            )
            if ($existingCancellation.Count -gt 0) {
                if ($existingCancellation.Count -ne 1 -or
                    $existingCancellation[0].reason -ne $trimmedCancellationReason) {
                    throw "Reservation $ReservationId has conflicting cancellation evidence."
                }
                Write-JsonOutput ([ordered]@{
                    gateId = $GateId
                    state = 'reservation_cancelled'
                    reason = $trimmedCancellationReason
                    job = $job
                })
                break
            }
            if ($job.status -ne 'reserved' -or $job.reservationId -ne $ReservationId) {
                throw "Job $JobKey does not hold the matching reservation."
            }

            $attempts = @($job.attempts)
            $job.cancellations = @($job.cancellations) + [ordered]@{
                reservationId = $ReservationId
                cancelledAtUtc = [DateTime]::UtcNow.ToString('o')
                reason = $trimmedCancellationReason
                priorStatus = 'reserved'
            }
            $job.status = if ($attempts.Count -gt 0) { 'failed' } else { 'planned' }
            $job.reservationId = $null
            $job.reservedAtUtc = $null
            $job.pendingSupersedesJobId = $null
            $job.pendingSupersedeReason = $null
            Write-GateState $gatePath $state
            Write-JsonOutput ([ordered]@{
                gateId = $GateId
                state = 'reservation_cancelled'
                reason = $trimmedCancellationReason
                job = $job
            })
        }
        'Validate' {
            $state = Read-GateState $gatePath $GateId
            Assert-StateRootUniqueEvidence $resolvedStateRoot
            $incomplete = @($state.jobs | Where-Object { $_.status -ne 'succeeded' })
            if (@($state.jobs).Count -eq 0 -or $incomplete.Count -gt 0) {
                $summary = ($incomplete | ForEach-Object { "$($_.key)=$($_.status)" }) -join ', '
                throw "Unity test gate is incomplete or failed: $summary"
            }
            Write-JsonOutput ([ordered]@{ gateId = $GateId; state = 'succeeded'; jobCount = @($state.jobs).Count })
        }
    }
}
