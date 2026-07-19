[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x86', 'win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
$fullPath = [System.IO.Path]::GetFullPath($MsiPath)
if (-not (Test-Path -LiteralPath $fullPath)) {
    throw "MSI package is missing: $fullPath"
}

$programFilesFolder = if ($RuntimeIdentifier -eq 'win-x86') { 'ProgramFilesFolder' } else { 'ProgramFiles64Folder' }
$perUserDefaultCondition = 'WixAppFolder = "WixPerUserFolder" AND NOT VELOPACK_INSTALLDIR'
$perMachineDefaultCondition = 'WixAppFolder = "WixPerMachineFolder" AND NOT VELOPACK_INSTALLDIR'

$installer = $null
$database = $null
try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember(
        'OpenDatabase',
        [System.Reflection.BindingFlags]::InvokeMethod,
        $null,
        $installer,
        @($fullPath, 1))

    $queries = @(
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'DesktopShortcut'",
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'BrowseInstallFolder'",
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'InstallFolderEdit'",
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'InstallFolderLabel'",
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'InstallDirectoryDlg'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallDirectoryDlg'",
        "DELETE FROM ``Dialog`` WHERE ``Dialog`` = 'InstallDirectoryDlg'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'BrowseInstallFolder'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'BothScopes' AND ``Event`` = '[INSTALLFOLDER]'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'Next' AND ``Event`` = '[INSTALLFOLDER]'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'Next' AND ``Event`` = 'NewDialog'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'VerifyReadyDlg' AND ``Control_`` = 'Back' AND ``Event`` = 'NewDialog' AND ``Condition`` = 'NOT Installed'",
        "DELETE FROM ``ControlEvent`` WHERE ``Dialog_`` = 'LicenseAgreementDlg' AND ``Control_`` = 'Next' AND ``Event`` = '[INSTALLFOLDER]'",
        "DELETE FROM ``CheckBox`` WHERE ``Property`` = 'INSTALLDESKTOPSHORTCUT'",
        "DELETE FROM ``Property`` WHERE ``Property`` = 'INSTALLDESKTOPSHORTCUT'",
        "UPDATE ``Control`` SET ``Control_Next`` = 'Back' WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'BothScopes'",
        "UPDATE ``Property`` SET ``Value`` = 'WixPerUserFolder' WHERE ``Property`` = 'WixAppFolder'",
        "INSERT INTO ``Property`` (``Property``, ``Value``) VALUES ('INSTALLDESKTOPSHORTCUT', '1')",
        "INSERT INTO ``CheckBox`` (``Property``, ``Value``) VALUES ('INSTALLDESKTOPSHORTCUT', '1')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Property``, ``Text``, ``Control_Next``) VALUES ('InstallScopeDlg', 'DesktopShortcut', 'CheckBox', 20, 185, 330, 18, 3, 'INSTALLDESKTOPSHORTCUT', '[MsiDesktopShortcutDescription]', 'Back')",
        "UPDATE ``Control`` SET ``Control_Next`` = 'DesktopShortcut' WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'BothScopes'",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallScopeDlg', 'Next', '[INSTALLFOLDER]', '[LocalAppDataFolder][ApplicationFolderName]', '$perUserDefaultCondition', 4)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallScopeDlg', 'Next', '[INSTALLFOLDER]', '[$programFilesFolder][ApplicationFolderName]', '$perMachineDefaultCondition', 5)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallScopeDlg', 'Next', '[INSTALLFOLDER]', '[VELOPACK_INSTALLDIR]', 'VELOPACK_INSTALLDIR', 5)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallScopeDlg', 'Next', 'NewDialog', 'InstallDirectoryDlg', '1', 8)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('VerifyReadyDlg', 'Back', 'NewDialog', 'InstallDirectoryDlg', 'NOT Installed', 1)",
        "INSERT INTO ``Dialog`` (``Dialog``, ``HCentering``, ``VCentering``, ``Width``, ``Height``, ``Attributes``, ``Title``, ``Control_First``, ``Control_Default``, ``Control_Cancel``) VALUES ('InstallDirectoryDlg', 50, 50, 370, 270, 23, '[MsiDlgTitle]', 'BannerBitmap', 'Next', 'Cancel')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'BannerBitmap', 'Bitmap', 0, 0, 370, 44, 1, 'WixUI_Bmp_Banner', 'BannerLine')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'BannerLine', 'Line', 0, 44, 370, 0, 1, 'BottomLine')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, 'Description')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'Description', 'Text', 25, 23, 280, 15, 3, '[MsiBrowseDescription]', 'Title')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'Title', 'Text', 15, 6, 200, 15, 3, '[MsiBrowseTitle]', 'PathLabel')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'PathLabel', 'Text', 25, 78, 320, 12, 3, '[MsiBrowsePathLabel]', 'InstallFolderEdit')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Property``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'InstallFolderEdit', 'PathEdit', 25, 94, 260, 18, 7, 'INSTALLFOLDER', 'BrowseInstallFolder')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'BrowseInstallFolder', 'PushButton', 293, 94, 52, 18, 3, '...', 'Back')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'Back', 'PushButton', 180, 243, 56, 17, 3, '[MsiBtnBack]', 'Next')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'Next', 'PushButton', 236, 243, 56, 17, 3, '[MsiBtnNext]', 'Cancel')",
        "INSERT INTO ``Control`` (``Dialog_``, ``Control``, ``Type``, ``X``, ``Y``, ``Width``, ``Height``, ``Attributes``, ``Text``, ``Control_Next``) VALUES ('InstallDirectoryDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, '[MsiBtnCancel]', 'BannerBitmap')",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'BrowseInstallFolder', '[_BrowseProperty]', 'INSTALLFOLDER', '1', 1)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'BrowseInstallFolder', 'SpawnDialog', 'BrowseDlg', '1', 2)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'Back', 'NewDialog', 'InstallScopeDlg', '1', 1)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'Next', 'SetTargetPath', 'INSTALLFOLDER', '1', 1)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'Next', 'DoAction', 'RustValidatePath', 'NOT WIXUI_DONTVALIDATEPATH', 2)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'Next', 'NewDialog', 'VerifyReadyDlg', '1', 3)",
        "INSERT INTO ``ControlEvent`` (``Dialog_``, ``Control_``, ``Event``, ``Argument``, ``Condition``, ``Ordering``) VALUES ('InstallDirectoryDlg', 'Cancel', 'SpawnDialog', 'CancelDlg', '1', 1)",
        "UPDATE ``Component`` SET ``Condition`` = 'INSTALLDESKTOPSHORTCUT = 1' WHERE ``Component`` = 'ApplicationDesktopShortcut'"
    )

    foreach ($query in $queries) {
        $view = $null
        try {
            $view = $database.GetType().InvokeMember(
                'OpenView',
                [System.Reflection.BindingFlags]::InvokeMethod,
                $null,
                $database,
                @($query))
            $null = $view.GetType().InvokeMember(
                'Execute',
                [System.Reflection.BindingFlags]::InvokeMethod,
                $null,
                $view,
                $null)
        }
        catch {
            throw "MSI customization query failed: $query`n$($_.Exception.Message)"
        }
        finally {
            if ($null -ne $view) {
                $null = $view.GetType().InvokeMember(
                    'Close',
                    [System.Reflection.BindingFlags]::InvokeMethod,
                    $null,
                    $view,
                    $null)
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
            }
        }
    }

    $null = $database.GetType().InvokeMember(
        'Commit',
        [System.Reflection.BindingFlags]::InvokeMethod,
        $null,
        $database,
        $null)
}
finally {
    if ($null -ne $database) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
    }
    if ($null -ne $installer) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
    }
}
