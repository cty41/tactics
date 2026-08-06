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
$syncScript = Join-Path $PSScriptRoot 'Sync-ProjectMcpConfig.ps1'

switch ($PSCmdlet.ParameterSetName) {
    'PrepareMigration' {
        & $syncScript -Operation PrepareMigration -InitializeUrl $Url
    }
    'RestoreMigration' {
        & $syncScript -Operation RestoreMigration
    }
    default {
        & $syncScript -Operation Initialize -InitializeUrl $Url
    }
}

if (-not $?) {
    exit 1
}
