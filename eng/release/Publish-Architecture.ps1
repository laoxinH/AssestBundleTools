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

$configuration = switch ($RuntimeIdentifier) {
    'win-x86' { @{ Platform = 'x86'; PackId = 'AssestBundleTools.win-x86'; Machine = 0x014c } }
    'win-x64' { @{ Platform = 'x64'; PackId = 'AssestBundleTools.win-x64'; Machine = 0x8664 } }
    'win-arm64' { @{ Platform = 'ARM64'; PackId = 'AssestBundleTools.win-arm64'; Machine = 0xaa64 } }
}

if (Test-Path -LiteralPath $architectureOutput) {
    Remove-Item -LiteralPath $architectureOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory, $releaseDirectory -Force | Out-Null
Set-Content -LiteralPath $releaseNotesPath -Encoding utf8 -Value "AssestBundleTools $Version"

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
        --shortcuts 'StartMenuRoot' `
        --instLocation 'PerUser' `
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
$portable = Get-ChildItem -LiteralPath $releaseDirectory -Filter '*Portable.zip' | Select-Object -First 1
$feed = Join-Path $releaseDirectory "releases.$RuntimeIdentifier.json"
if ($null -eq $setup -or $null -eq $portable -or -not (Test-Path -LiteralPath $feed)) {
    throw 'Velopack output is incomplete: Setup, Portable, or release feed is missing.'
}

$setupName = "AssestBundleTools-$Version-$RuntimeIdentifier-Setup.exe"
$portableName = "AssestBundleTools-$Version-$RuntimeIdentifier-Portable.zip"
Move-Item -LiteralPath $setup.FullName -Destination (Join-Path $releaseDirectory $setupName) -Force
Move-Item -LiteralPath $portable.FullName -Destination (Join-Path $releaseDirectory $portableName) -Force

$hashLines = Get-ChildItem -LiteralPath $releaseDirectory -File |
    Sort-Object Name |
    ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
    }
Set-Content -LiteralPath (Join-Path $releaseDirectory "SHA256SUMS-$RuntimeIdentifier.txt") -Encoding ascii -Value $hashLines

Write-Host "Release output: $releaseDirectory"
