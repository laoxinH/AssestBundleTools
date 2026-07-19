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

function Get-MsiScalar {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Query
    )

    $view = $null
    $record = $null
    try {
        $view = $Database.GetType().InvokeMember(
            'OpenView',
            [System.Reflection.BindingFlags]::InvokeMethod,
            $null,
            $Database,
            @($Query))
        $null = $view.GetType().InvokeMember(
            'Execute',
            [System.Reflection.BindingFlags]::InvokeMethod,
            $null,
            $view,
            $null)
        $record = $view.GetType().InvokeMember(
            'Fetch',
            [System.Reflection.BindingFlags]::InvokeMethod,
            $null,
            $view,
            $null)
        if ($null -eq $record) {
            return $null
        }

        return $record.GetType().InvokeMember(
            'StringData',
            [System.Reflection.BindingFlags]::GetProperty,
            $null,
            $record,
            @(1))
    }
    finally {
        if ($null -ne $record) {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
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

$installer = $null
$database = $null
try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember(
        'OpenDatabase',
        [System.Reflection.BindingFlags]::InvokeMethod,
        $null,
        $installer,
        @($fullPath, 0))

    $checks = @(
        @{
            Name = 'license agreement dialog'
            Query = "SELECT ``Dialog`` FROM ``Dialog`` WHERE ``Dialog`` = 'LicenseAgreementDlg'"
            Expected = 'LicenseAgreementDlg'
        },
        @{
            Name = 'embedded license content'
            Query = "SELECT ``Text`` FROM ``Control`` WHERE ``Dialog_`` = 'LicenseAgreementDlg' AND ``Control`` = 'LicenseText'"
            Contains = 'MIT License'
        },
        @{
            Name = 'install scope dialog'
            Query = "SELECT ``Dialog`` FROM ``Dialog`` WHERE ``Dialog`` = 'InstallScopeDlg'"
            Expected = 'InstallScopeDlg'
        },
        @{
            Name = 'desktop shortcut default'
            Query = "SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'INSTALLDESKTOPSHORTCUT'"
            Expected = '1'
        },
        @{
            Name = 'desktop shortcut checkbox'
            Query = "SELECT ``Text`` FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'DesktopShortcut'"
            Expected = '[MsiDesktopShortcutDescription]'
        },
        @{
            Name = 'desktop shortcut keyboard navigation'
            Query = "SELECT ``Control_Next`` FROM ``Control`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control`` = 'BothScopes'"
            Expected = 'DesktopShortcut'
        },
        @{
            Name = 'current-user default scope'
            Query = "SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'WixAppFolder'"
            Expected = 'WixPerUserFolder'
        },
        @{
            Name = 'install location editor'
            Query = "SELECT ``Property`` FROM ``Control`` WHERE ``Dialog_`` = 'InstallDirectoryDlg' AND ``Control`` = 'InstallFolderEdit'"
            Expected = 'INSTALLFOLDER'
        },
        @{
            Name = 'install location browser'
            Query = "SELECT ``Argument`` FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallDirectoryDlg' AND ``Control_`` = 'BrowseInstallFolder' AND ``Event`` = 'SpawnDialog'"
            Expected = 'BrowseDlg'
        },
        @{
            Name = 'scope to install location navigation'
            Query = "SELECT ``Argument`` FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'Next' AND ``Event`` = 'NewDialog'"
            Expected = 'InstallDirectoryDlg'
        },
        @{
            Name = 'ready page back navigation'
            Query = "SELECT ``Argument`` FROM ``ControlEvent`` WHERE ``Dialog_`` = 'VerifyReadyDlg' AND ``Control_`` = 'Back' AND ``Event`` = 'NewDialog' AND ``Condition`` = 'NOT Installed'"
            Expected = 'InstallDirectoryDlg'
        },
        @{
            Name = 'per-machine default install folder'
            Query = "SELECT ``Argument`` FROM ``ControlEvent`` WHERE ``Dialog_`` = 'InstallScopeDlg' AND ``Control_`` = 'Next' AND ``Event`` = '[INSTALLFOLDER]' AND ``Condition`` = 'WixAppFolder = `"WixPerMachineFolder`" AND NOT VELOPACK_INSTALLDIR'"
            Expected = "[$programFilesFolder][ApplicationFolderName]"
        },
        @{
            Name = 'desktop shortcut component condition'
            Query = "SELECT ``Condition`` FROM ``Component`` WHERE ``Component`` = 'ApplicationDesktopShortcut'"
            Expected = 'INSTALLDESKTOPSHORTCUT = 1'
        }
    )

    foreach ($check in $checks) {
        $actual = Get-MsiScalar -Database $database -Query $check.Query
        $expectedContent = $check['Contains']
        $expectedValue = $check['Expected']
        if ($expectedContent -and -not $actual.Contains($expectedContent, [System.StringComparison]::Ordinal)) {
            throw "MSI contract check failed for $($check.Name). Expected content '$expectedContent' was not found."
        }
        if ($expectedValue -and $actual -ne $expectedValue) {
            throw "MSI contract check failed for $($check.Name). Expected '$expectedValue', actual '$actual'."
        }
    }
}
finally {
    if ($null -ne $database) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
    }
    if ($null -ne $installer) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
    }
}

Write-Host "MSI installer contract verified: $fullPath"
