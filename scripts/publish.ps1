#requires -Version 7.0
<#
.SYNOPSIS
    Produce self-contained single-file artifacts for StreamManager (Windows + macOS).

.DESCRIPTION
    Generates the app icon from streammanager.png, runs `dotnet publish` for
    each requested RID, assembles a macOS .app bundle where applicable, and
    optionally verifies the output.

.PARAMETER Rid
    One or more RIDs to publish. Defaults to win-x64, osx-arm64, osx-x64.

.PARAMETER Verify
    Run post-publish sanity checks (artifact shape, Info.plist version, no
    stray config.json / *.log files).

.PARAMETER Clean
    Wipe the artifacts/ directory before publishing.

.EXAMPLE
    pwsh scripts/publish.ps1 -Verify

.EXAMPLE
    pwsh scripts/publish.ps1 -Rid win-x64 -Verify
#>
[CmdletBinding()]
param(
    [string[]]$Rid = @('win-x64', 'osx-arm64', 'osx-x64'),
    [switch]$Verify,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$RepoRoot      = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $RepoRoot

$VersionFile   = Join-Path $RepoRoot 'VERSION'
if (-not (Test-Path $VersionFile)) {
    throw "VERSION file not found at $VersionFile"
}
$Version       = (Get-Content $VersionFile -Raw).Trim()

$AppProject    = Join-Path $RepoRoot 'src/StreamManager.App/StreamManager.App.csproj'
$IconSource    = Join-Path $RepoRoot 'streammanager.png'
$IconDir       = Join-Path $RepoRoot 'build/icons'
$IconIco       = Join-Path $IconDir  'streammanager.ico'
$IconIcns      = Join-Path $IconDir  'streammanager.icns'
$PlistTemplate = Join-Path $RepoRoot 'build/mac/Info.plist.template'
$ArtifactsDir  = Join-Path $RepoRoot 'artifacts'

if (-not (Test-Path $IconSource)) {
    throw "Source icon not found: $IconSource"
}

function Write-Log([string]$msg) { Write-Host "[publish] $msg" }

function Build-MacAppBundle([string]$RidDir, [string]$RidName) {
    $appDir   = Join-Path $RidDir 'StreamManager.app'
    $contents = Join-Path $appDir 'Contents'
    Write-Log "  assembling StreamManager.app for $RidName"

    if (Test-Path $appDir) { Remove-Item -Recurse -Force $appDir }
    New-Item -ItemType Directory -Path (Join-Path $contents 'MacOS')     | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $contents 'Resources') | Out-Null

    # Move everything publish emitted into Contents/MacOS (except the future
    # .app dir itself). Single-file publish usually emits StreamManager plus
    # createdump; we don't filter so future platform assets go along for the ride.
    Get-ChildItem -LiteralPath $RidDir -Force | ForEach-Object {
        if ($_.FullName -eq $appDir) { return }
        Move-Item -LiteralPath $_.FullName -Destination (Join-Path $contents 'MacOS')
    }

    Copy-Item $IconIcns (Join-Path $contents 'Resources/streammanager.icns')

    $plist = (Get-Content $PlistTemplate -Raw) -replace '{{VERSION}}', $Version
    Set-Content -Path (Join-Path $contents 'Info.plist') -Value $plist -NoNewline

    # 8-byte magic Finder uses to recognise the bundle type.
    [System.IO.File]::WriteAllBytes(
        (Join-Path $contents 'PkgInfo'),
        [Text.Encoding]::ASCII.GetBytes('APPL????'))
}

if ($Clean -and (Test-Path $ArtifactsDir)) {
    Write-Log 'cleaning artifacts/'
    Remove-Item -Recurse -Force $ArtifactsDir
}

Write-Log "generating app icons from $IconSource (version $Version)"
& dotnet build (Join-Path $RepoRoot 'build/IconGen/IconGen.csproj') -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'IconGen build failed' }
$IconGenDll = Join-Path $RepoRoot 'build/IconGen/bin/Release/net10.0/IconGen.dll'
& dotnet $IconGenDll $IconSource $IconIco $IconIcns
if ($LASTEXITCODE -ne 0) { throw 'IconGen run failed' }

foreach ($r in $Rid) {
    $out = Join-Path $ArtifactsDir $r
    Write-Log "publishing $r -> $out"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    New-Item -ItemType Directory -Path $out | Out-Null

    & dotnet publish $AppProject `
        -c Release `
        -r $r `
        --self-contained true `
        -o $out `
        -p:Version=$Version `
        -p:InformationalVersion=$Version `
        --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $r" }

    # Native-lib PDBs (libSkiaSharp.pdb, libHarfBuzzSharp.pdb) get copied
    # alongside the win-x64 native DLLs. CopyDebugSymbolFilesFromPackages
    # doesn't catch them because they live under runtimes/<rid>/native/.
    Get-ChildItem -Path $out -Recurse -File -Filter '*.pdb' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    if ($r -like 'osx-*') {
        Build-MacAppBundle $out $r
    }
}

if ($Verify) {
    Write-Log 'verifying artifacts'
    $fail = $false

    $leaks = Get-ChildItem -Path $ArtifactsDir -Recurse -File -Force `
        -Include 'config.json', '*.log' -ErrorAction SilentlyContinue
    if ($leaks) {
        Write-Error "forbidden runtime files found in artifacts:`n$($leaks.FullName -join [Environment]::NewLine)"
        $fail = $true
    }

    foreach ($r in $Rid) {
        $out = Join-Path $ArtifactsDir $r
        if ($r -like 'win-*') {
            $exe = Join-Path $out 'StreamManager.exe'
            if (-not (Test-Path $exe) -or (Get-Item $exe).Length -eq 0) {
                Write-Error "missing or empty $exe"; $fail = $true; continue
            }
            $bytes = [System.IO.File]::ReadAllBytes($exe) | Select-Object -First 2
            if (-not ($bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A)) {
                Write-Error "$exe does not have PE32 'MZ' magic"; $fail = $true
            } else {
                Write-Log "  OK $r: $(Split-Path -Leaf $exe) is PE32"
            }
        }
        elseif ($r -like 'osx-*') {
            $app   = Join-Path $out 'StreamManager.app'
            $plist = Join-Path $app 'Contents/Info.plist'
            $bin   = Join-Path $app 'Contents/MacOS/StreamManager'
            $icns  = Join-Path $app 'Contents/Resources/streammanager.icns'
            if (-not (Test-Path $app))   { Write-Error "missing $app";   $fail = $true; continue }
            if (-not (Test-Path $bin)  -or (Get-Item $bin).Length  -eq 0) { Write-Error "missing or empty $bin";  $fail = $true }
            if (-not (Test-Path $icns) -or (Get-Item $icns).Length -eq 0) { Write-Error "missing or empty $icns"; $fail = $true }
            if (-not (Select-String -Path $plist -Pattern "<string>$Version</string>" -Quiet)) {
                Write-Error "$plist does not contain version $Version"; $fail = $true
            } else {
                Write-Log "  OK ${r}: Info.plist version=$Version, .icns present"
            }
        }
    }

    if ($fail) { throw 'verify failed' }
    Write-Log 'verify OK'
}

Write-Log "done. artifacts in $ArtifactsDir/"
