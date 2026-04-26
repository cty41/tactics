#requires -Version 5.1
<#
.SYNOPSIS
    Shows a Windows 10/11 toast notification via WinRT.
.DESCRIPTION
    Requires Ensure-ToastAppId.ps1 to have been run first.
#>
param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateNotNullOrEmpty()]
    [string] $Title,

    [Parameter(Mandatory=$false, Position=1)]
    [Alias('Body','Text')]
    [string[]] $Message = @(),

    [Parameter()]
    [string] $AppId = 'Tactics.Unity.Editor',

    [Parameter()]
    [switch] $Silent,

    [Parameter()]
    [string] $Duration = 'Short'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Escape-XmlText {
    param([string] $Text)
    if ($null -eq $Text) { return '' }
    $Text = $Text -replace '&', '__AMP__'
    $Text = $Text -replace '<', '__LT__'
    $Text = $Text -replace '>', '__GT__'
    $Text = $Text -replace '"', '__QUOT__'
    $Text = $Text -replace '__AMP__', '__AMP_PLACEHOLDER__'
    $Text = $Text -replace '__LT__', '__LT_PLACEHOLDER__'
    $Text = $Text -replace '__GT__', '__GT_PLACEHOLDER__'
    $Text = $Text -replace '__QUOT__', '__QUOT_PLACEHOLDER__'
    return $Text
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$bodyText = if ($Message.Count -gt 0) { $Message -join [Environment]::NewLine } else { '' }
$titleEsc = Escape-XmlText -Text $Title.Trim()
$bodyEsc = Escape-XmlText -Text $bodyText

$lt = [char]60
$gt = [char]62
$q = [char]34
$amp = [char]38
$ampLt = $amp + "amp;"
$ampGt = $amp + "gt;"
$ampQuot = $amp + "quot;"
$ltEntity = $amp + "lt;"

$bodyEsc = $bodyEsc -replace '__AMP_PLACEHOLDER__', $ampLt
$bodyEsc = $bodyEsc -replace '__LT_PLACEHOLDER__', $ltEntity
$bodyEsc = $bodyEsc -replace '__GT_PLACEHOLDER__', $ampGt
$bodyEsc = $bodyEsc -replace '__QUOT_PLACEHOLDER__', $ampQuot
$titleEsc = $titleEsc -replace '__AMP_PLACEHOLDER__', $ampLt
$titleEsc = $titleEsc -replace '__LT_PLACEHOLDER__', $ltEntity
$titleEsc = $titleEsc -replace '__GT_PLACEHOLDER__', $ampGt
$titleEsc = $titleEsc -replace '__QUOT_PLACEHOLDER__', $ampQuot

$xmlString = $lt+"toast duration="+$q+$Duration.ToLower()+$q+$gt+
    $lt+"visual"+$gt+
    $lt+"binding template="+$q+"ToastGeneric"+$q+$gt+
    $lt+"text hint-maxLines="+$q+"2"+$q+$gt+$titleEsc+$lt+"/text"+$gt+
    $lt+"text hint-style="+$q+"body"+$q+" hint-wrap="+$q+"true"+$q+" hint-maxLines="+$q+"4"+$q+$gt+$bodyEsc+$lt+"/text"+$gt+
    $lt+"/binding"+$gt+
    $lt+"/visual"+$gt+
    $lt+"audio silent="+$q+"true"+$q+"/"+$gt+
    $lt+"/toast"+$gt

Write-Host "XML: $xmlString"

$xmlDoc = New-Object Windows.Data.Xml.Dom.XmlDocument
$xmlDoc.LoadXml($xmlString)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xmlDoc)
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($AppId)
$notifier.Show($toast)
Write-Host "Toast shown"
