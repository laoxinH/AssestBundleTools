[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x86', 'win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier,

    [string]$OutputRoot = 'artifacts/release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectPath = Join-Path $repoRoot 'src\AssestBundleTools.Presentation.WinUI\AssestBundleTools.Presentation.WinUI.csproj'
$outputRootPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$architectureOutput = Join-Path $outputRootPath $RuntimeIdentifier
$publishDirectory = Join-Path $architectureOutput 'publish'
$releaseDirectory = Join-Path $architectureOutput 'release'
$releaseNotesPath = Join-Path $architectureOutput 'release-notes.md'
$versionedReleaseNotesPath = Join-Path $repoRoot "docs\releases\v$Version.md"
$installerWelcomePath = Join-Path $repoRoot 'eng\installer\welcome.txt'
$installerReadmePath = Join-Path $repoRoot 'eng\installer\readme.md'
$installerLicensePath = Join-Path $repoRoot 'LICENSE.md'
$setMsiInstallerOptionsPath = Join-Path $repoRoot 'eng\release\Set-MsiInstallerOptions.ps1'
$testMsiInstallerContractPath = Join-Path $repoRoot 'eng\release\Test-MsiInstallerContract.ps1'
$monacoSourceDirectory = Join-Path $repoRoot 'src\AssestBundleTools.Presentation.WinUI\Assets\Monaco'

if (-not (Test-Path -LiteralPath $monacoSourceDirectory -PathType Container)) {
    throw "Monaco source directory is missing: $monacoSourceDirectory"
}

$requiredMonacoAssetRelativePaths = @(
    Get-ChildItem -LiteralPath $monacoSourceDirectory -File -Recurse |
        ForEach-Object {
            [System.IO.Path]::GetRelativePath($monacoSourceDirectory, $_.FullName).Replace('\', '/')
        } |
        Sort-Object
)
if ($requiredMonacoAssetRelativePaths.Count -eq 0) {
    throw 'The Monaco source directory does not contain any files.'
}

function Assert-MonacoAssetDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssetRoot,

        [Parameter(Mandatory = $true)]
        [string]$Context
    )

    foreach ($relativePath in $requiredMonacoAssetRelativePaths) {
        $assetPath = Join-Path $AssetRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "$Context is missing the Monaco asset: Assets/Monaco/$relativePath"
        }
    }
}

function Assert-MonacoAssetsInArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$Context
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($relativePath in $requiredMonacoAssetRelativePaths) {
            $expectedPath = "Assets/Monaco/$relativePath"
            $hasEntry = $entryNames.Where({
                $_.Equals($expectedPath, [System.StringComparison]::OrdinalIgnoreCase) -or
                $_.EndsWith("/$expectedPath", [System.StringComparison]::OrdinalIgnoreCase)
            }, 'First').Count -gt 0
            if (-not $hasEntry) {
                throw "$Context is missing the Monaco asset: $expectedPath"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$configuration = switch ($RuntimeIdentifier) {
    'win-x86' { @{ Platform = 'x86'; PackId = 'AssestBundleTools.win-x86'; Machine = 0x014c } }
    'win-x64' { @{ Platform = 'x64'; PackId = 'AssestBundleTools.win-x64'; Machine = 0x8664 } }
    'win-arm64' { @{ Platform = 'ARM64'; PackId = 'AssestBundleTools.win-arm64'; Machine = 0xaa64 } }
}

if (Test-Path -LiteralPath $architectureOutput) {
    Remove-Item -LiteralPath $architectureOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory, $releaseDirectory -Force | Out-Null
if (Test-Path -LiteralPath $versionedReleaseNotesPath) {
    Copy-Item -LiteralPath $versionedReleaseNotesPath -Destination $releaseNotesPath
}
else {
    Set-Content -LiteralPath $releaseNotesPath -Encoding utf8 -Value "AssestBundleTools $Version"
}

Push-Location $repoRoot
try {
    dotnet restore $projectPath `
        -p:Platform=$($configuration.Platform) `
        -p:PublishReadyToRun=true `
        -r $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet publish $projectPath `
        --configuration Release `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        --no-restore `
        -p:Platform=$($configuration.Platform) `
        -p:PublishDir=$publishDirectory `
        -p:MinVerVersionOverride=$Version
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    Assert-MonacoAssetDirectory `
        -AssetRoot (Join-Path $publishDirectory 'Assets\Monaco') `
        -Context "$RuntimeIdentifier publish output"

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet vpk pack `
        --packId $configuration.PackId `
        --packVersion $Version `
        --packDir $publishDirectory `
        --mainExe 'AssestBundleTools.Presentation.WinUI.exe' `
        --runtime $RuntimeIdentifier `
        --channel $RuntimeIdentifier `
        --packTitle 'AssestBundleTools' `
        --packAuthors 'laoxinH' `
        --releaseNotes $releaseNotesPath `
        --icon (Join-Path $publishDirectory 'Assets\AppIcon.ico') `
        --shortcuts 'Desktop,StartMenuRoot' `
        --msi true `
        --instWelcome $installerWelcomePath `
        --instLicense $installerLicensePath `
        --instReadme $installerReadmePath `
        --instLocation 'Either' `
        --outputDir $releaseDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Velopack packaging failed.' }
}
finally {
    Pop-Location
}

$mainExe = Join-Path $publishDirectory 'AssestBundleTools.Presentation.WinUI.exe'
if (-not (Test-Path -LiteralPath $mainExe)) {
    throw "Published executable is missing: $mainExe"
}

$stream = [System.IO.File]::OpenRead($mainExe)
$reader = [System.IO.BinaryReader]::new($stream)
try {
    $stream.Position = 0x3c
    $peOffset = $reader.ReadInt32()
    $stream.Position = $peOffset + 4
    $machine = $reader.ReadUInt16()
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}

if ($machine -ne $configuration.Machine) {
    throw ('PE architecture mismatch. Expected 0x{0:x4}, actual 0x{1:x4}.' -f $configuration.Machine, $machine)
}

$productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($mainExe).ProductVersion
if ($productVersion -ne $Version) {
    throw "Published product version mismatch. Expected $Version, actual $productVersion."
}

$setup = Get-ChildItem -LiteralPath $releaseDirectory -Filter '*Setup.exe' | Select-Object -First 1
$installerMsi = Get-ChildItem -LiteralPath $releaseDirectory -Filter '*.msi' | Select-Object -First 1
$portable = Get-ChildItem -LiteralPath $releaseDirectory -Filter '*Portable.zip' | Select-Object -First 1
$fullPackage = Get-ChildItem -LiteralPath $releaseDirectory -Filter '*full.nupkg' | Select-Object -First 1
$feed = Join-Path $releaseDirectory "releases.$RuntimeIdentifier.json"
if ($null -eq $setup -or
    $null -eq $installerMsi -or
    $null -eq $portable -or
    $null -eq $fullPackage -or
    -not (Test-Path -LiteralPath $feed)) {
    throw 'Velopack output is incomplete: Setup, MSI, Portable, full package, or release feed is missing.'
}

Assert-MonacoAssetsInArchive -ArchivePath $portable.FullName -Context "$RuntimeIdentifier portable package"
Assert-MonacoAssetsInArchive -ArchivePath $fullPackage.FullName -Context "$RuntimeIdentifier full update package"

$installerName = "AssestBundleTools-$Version-$RuntimeIdentifier-Installer.msi"
$portableName = "AssestBundleTools-$Version-$RuntimeIdentifier-Portable.zip"
& $setMsiInstallerOptionsPath -MsiPath $installerMsi.FullName -RuntimeIdentifier $RuntimeIdentifier
& $testMsiInstallerContractPath -MsiPath $installerMsi.FullName -RuntimeIdentifier $RuntimeIdentifier
Remove-Item -LiteralPath $setup.FullName -Force
Move-Item -LiteralPath $installerMsi.FullName -Destination (Join-Path $releaseDirectory $installerName) -Force
Move-Item -LiteralPath $portable.FullName -Destination (Join-Path $releaseDirectory $portableName) -Force

$assetIndexPath = Join-Path $releaseDirectory "assets.$RuntimeIdentifier.json"
$assetIndex = @(Get-Content -LiteralPath $assetIndexPath -Raw | ConvertFrom-Json)
$publishedAssets = foreach ($asset in $assetIndex) {
    if ($asset.Type -eq 'Installer') {
        continue
    }
    switch ($asset.Type) {
        'Msi' { $asset.RelativeFileName = $installerName }
        'Portable' { $asset.RelativeFileName = $portableName }
    }
    $asset
}
$publishedAssets | ConvertTo-Json -Compress | Set-Content -LiteralPath $assetIndexPath -Encoding utf8
if (@($publishedAssets).Count -ne 3) {
    throw "Release asset index should contain MSI, Portable, and Full entries; found $(@($publishedAssets).Count)."
}
foreach ($asset in $publishedAssets) {
    $assetPath = Join-Path $releaseDirectory $asset.RelativeFileName
    if (-not (Test-Path -LiteralPath $assetPath)) {
        throw "Release asset index references a missing file: $($asset.RelativeFileName)"
    }
}

$hashLines = Get-ChildItem -LiteralPath $releaseDirectory -File |
    Sort-Object Name |
    ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
    }
Set-Content -LiteralPath (Join-Path $releaseDirectory "SHA256SUMS-$RuntimeIdentifier.txt") -Encoding ascii -Value $hashLines

Write-Host "Release output: $releaseDirectory"
