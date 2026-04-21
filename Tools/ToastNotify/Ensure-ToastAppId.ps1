#requires -Version 5.1
<#
.SYNOPSIS
    Creates or repairs a Start Menu shortcut with System.AppUserModel.ID for WinRT toast attribution.
.DESCRIPTION
    WinRT CreateToastNotifier(AppUserModelId) resolves the notifier from a Start Menu shortcut whose
    AppUserModelID property matches. WScript.Shell alone cannot set that property; this script writes
    the .lnk via WScript, then sets PKEY_AppUserModel_ID through the shell property store API.
.PARAMETER AppId
    Application User Model ID string (e.g. Tactics.Unity.Editor).
.PARAMETER ShortcutName
    File name without extension; output is <name>.lnk under the user's Programs folder.
.PARAMETER TargetPath
    Full path to the executable (Unity Editor exe).
.PARAMETER WorkingDirectory
    Working directory for the shortcut (typically the editor install folder).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $AppId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ShortcutName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $TargetPath,

    [Parameter(Mandatory = $true)]
    [string] $WorkingDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $TargetPath -PathType Leaf)) {
    throw "TargetPath is not a file: $TargetPath"
}

$programsDir = Join-Path -Path $env:APPDATA -ChildPath 'Microsoft\Windows\Start Menu\Programs'
if (-not (Test-Path -LiteralPath $programsDir -PathType Container)) {
    New-Item -ItemType Directory -Path $programsDir -Force | Out-Null
}

$baseName = $ShortcutName.Trim()
if ($baseName.EndsWith('.lnk', [StringComparison]::OrdinalIgnoreCase)) {
    $baseName = $baseName.Substring(0, $baseName.Length - 4)
}
$shortcutPath = Join-Path -Path $programsDir -ChildPath ($baseName + '.lnk')

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($shortcutPath)
$sc.TargetPath = $TargetPath
$sc.WorkingDirectory = if ([string]::IsNullOrEmpty($WorkingDirectory)) { '' } else { $WorkingDirectory }
$sc.Save()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($sc) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null

$interopSource = @'
using System;
using System.Runtime.InteropServices;

namespace Tactics.ToastInterop {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PropVariant {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("0000010b-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        [PreserveSig] int GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    public static class ShellPropertyStore {
        private static readonly Guid CLSID_ShellLink = new Guid("00021401-0000-0000-C000-000000000046");

        private static PROPERTYKEY MakeAppUserModelKey() {
            return new PROPERTYKEY {
                fmtid = new Guid("9F4C2855-9E79-4B39-A8D0-E1D42DE1D5F3"),
                pid = 5
            };
        }

        private const uint STGM_READWRITE = 0x00000002;
        private const ushort VT_LPWSTR = 31;

        [DllImport("ole32.dll", PreserveSig = true)]
        private static extern int PropVariantClear(ref PropVariant propvar);

        public static void SetAppUserModelId(string shortcutPath, string appId) {
            if (string.IsNullOrEmpty(shortcutPath)) throw new ArgumentException("shortcutPath");
            if (string.IsNullOrEmpty(appId)) throw new ArgumentException("appId");

            Type linkType = Type.GetTypeFromCLSID(CLSID_ShellLink, true);
            object linkObj = Activator.CreateInstance(linkType);
            var pf = (IPersistFile)linkObj;
            int hr = pf.Load(shortcutPath, STGM_READWRITE);
            if (hr < 0) {
                throw new InvalidOperationException("IPersistFile.Load failed HRESULT=0x" + hr.ToString("X8"));
            }

            var store = (IPropertyStore)linkObj;
            PROPERTYKEY pkey = MakeAppUserModelKey();
            IntPtr mem = Marshal.StringToCoTaskMemUni(appId);
            var pv = new PropVariant { vt = VT_LPWSTR, pointerValue = mem };
            try {
                hr = store.SetValue(ref pkey, ref pv);
                if (hr < 0) {
                    throw new InvalidOperationException("IPropertyStore.SetValue(AppUserModelID) failed HRESULT=0x" + hr.ToString("X8"));
                }
                hr = store.Commit();
                if (hr < 0) {
                    throw new InvalidOperationException("IPropertyStore.Commit failed HRESULT=0x" + hr.ToString("X8"));
                }
                hr = pf.Save(shortcutPath, true);
                if (hr < 0) {
                    throw new InvalidOperationException("IPersistFile.Save failed HRESULT=0x" + hr.ToString("X8"));
                }
            }
            finally {
                PropVariantClear(ref pv);
            }
        }
    }
}
'@

$interopTypeName = 'Tactics.ToastInterop.ShellPropertyStore'
try {
    if (-not ($interopTypeName -as [type])) {
        Add-Type -TypeDefinition $interopSource -Language CSharp -ErrorAction Stop
    }
}
catch {
    throw "Failed to compile interop types: $($_.Exception.Message)"
}

try {
    [Tactics.ToastInterop.ShellPropertyStore]::SetAppUserModelId($shortcutPath, $AppId)
}
catch {
    throw "Failed to set AppUserModelID on shortcut: $($_.Exception.Message)"
}