Param(
    [string]$dllPath,
    [string]$appId,
    [string]$title,
    [string]$content
)

Add-Type -Path $dllPath

[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]

[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]

$template = @"
<toast>
    <visual>
        <binding template="ToastGeneric">
            <text id="1">$title</text>
            <text id="2">$content</text>
        </binding>
    </visual>
</toast>
"@

$toastXml = New-Object Windows.Data.Xml.Dom.XmlDocument
$toastXml.LoadXml($template)
$toast = [Windows.UI.Notifications.ToastNotification]::new($toastXml)

$toastNotifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId)
$toastNotifier.Show($toast)
