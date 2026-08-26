<#
Fetches the pinned Godot 4.7 .NET (mono) editor named in godot.lock into .godot-editor/ (gitignored),
verifying the downloaded zip's SHA512 against the lock before extracting it. Digests are copied from
the godotengine/godot-builds release's own SHA512-SUMS.txt asset, not recomputed locally.

    just godot-fetch                 fetch the pinned win64 mono editor
    just godot-fetch -Templates      also fetch + install the matching mono export templates
    just godot-fetch -Force          re-fetch even when the installed tree already matches the pin

Point $env:GODOT_BIN at an existing Godot 4.7 mono install to skip the editor fetch entirely -- every
recipe resolves the binary through tools/godot-resolve.ps1, which checks GODOT_BIN first. Install the
editor somewhere other than .godot-editor/ (a cache shared across projects, say) with
$env:SPACECRAFT_GODOT_HOME.
#>
[CmdletBinding()]
param(
    [switch]$Templates,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$lock = Get-Content -LiteralPath (Join-Path $root "godot.lock") -Raw | ConvertFrom-Json

function Get-Sha512Hex([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash.ToLowerInvariant()
}

function Fetch-Verified([string]$AssetName, [string]$DestPath) {
    $digest = $lock.assets.$AssetName
    if (-not $digest) { throw "godot-fetch: $AssetName has no pinned digest in godot.lock" }
    $url = "https://github.com/$($lock.repo)/releases/download/$($lock.tag)/$AssetName"
    Write-Host "godot-fetch: downloading $AssetName"
    Invoke-WebRequest -Uri $url -OutFile $DestPath -UseBasicParsing
    $actual = Get-Sha512Hex $DestPath
    if ($actual -ne $digest.ToLowerInvariant()) {
        Remove-Item -LiteralPath $DestPath -Force
        throw "godot-fetch: $AssetName sha512 mismatch`n  expected $digest`n  got      $actual"
    }
}

$lockHash = Get-Sha512Hex (Join-Path $root "godot.lock")

# --- editor -----------------------------------------------------------------
$installDir = if ($env:SPACECRAFT_GODOT_HOME) { $env:SPACECRAFT_GODOT_HOME } else { Join-Path $root ".godot-editor" }
$stamp = Join-Path $installDir ".fetched"
$wantStamp = "$($lock.tag) editor $lockHash"

$alreadyInstalled = (-not $Force) -and (Test-Path -LiteralPath $stamp) -and
    ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $wantStamp)

if ($alreadyInstalled) {
    Write-Host "godot-fetch: $($lock.tag) editor already installed, skipping (pass -Force to re-fetch)"
} else {
    if (Test-Path -LiteralPath $stamp) { Remove-Item -LiteralPath $stamp -Force }
    $editorAsset = "Godot_v$($lock.tag)_mono_win64.zip"
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $zipPath = Join-Path $work $editorAsset
        Fetch-Verified $editorAsset $zipPath

        if (Test-Path -LiteralPath $installDir) { Remove-Item -LiteralPath $installDir -Recurse -Force }
        New-Item -ItemType Directory -Path $installDir | Out-Null
        Expand-Archive -LiteralPath $zipPath -DestinationPath $installDir -Force

        Set-Content -LiteralPath $stamp -Value $wantStamp -NoNewline
        Write-Host "godot-fetch: installed $($lock.tag) editor into $installDir"
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not $Templates) { exit 0 }

# --- export templates --------------------------------------------------------
# The install directory name is whatever THIS binary reports on --version, minus the trailing
# ".official.<hash>" build metadata -- that string, not the release tag, is what Godot looks for
# under %APPDATA%\Godot\export_templates\, and a .NET build's version string carries its own ".mono"
# marker (e.g. "4.7.stable.mono").
$godotExe = & (Join-Path $PSScriptRoot "godot-resolve.ps1")
if (-not $godotExe) {
    throw "godot-fetch: no Godot editor resolved (set GODOT_BIN, or drop -Templates and fetch the editor first)"
}
$rawVersion = (& $godotExe --version).Trim()
$parts = $rawVersion -split '\.'
$officialIdx = [array]::IndexOf($parts, "official")
$versionDirName = if ($officialIdx -gt 0) { ($parts[0..($officialIdx - 1)] -join '.') } else { $rawVersion }

$templatesDir = Join-Path (Join-Path $env:APPDATA "Godot\export_templates") $versionDirName
$templatesStamp = Join-Path $templatesDir ".fetched"
$wantTemplatesStamp = "$($lock.tag) templates $lockHash"

$templatesAlreadyInstalled = (-not $Force) -and (Test-Path -LiteralPath $templatesStamp) -and
    ((Get-Content -LiteralPath $templatesStamp -Raw).Trim() -eq $wantTemplatesStamp)

if ($templatesAlreadyInstalled) {
    Write-Host "godot-fetch: export templates for $versionDirName already installed, skipping"
    exit 0
}

$templatesAsset = "Godot_v$($lock.tag)_mono_export_templates.tpz"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $tpzPath = Join-Path $work $templatesAsset
    Fetch-Verified $templatesAsset $tpzPath

    if (Test-Path -LiteralPath $templatesDir) { Remove-Item -LiteralPath $templatesDir -Recurse -Force }
    New-Item -ItemType Directory -Path $templatesDir | Out-Null
    # A .tpz is a zip with everything under one top-level templates/ folder; Godot expects the
    # CONTENTS of that folder directly inside the version directory, not the wrapper itself.
    Expand-Archive -LiteralPath $tpzPath -DestinationPath $work -Force
    Copy-Item -Path (Join-Path $work "templates\*") -Destination $templatesDir -Recurse -Force

    Set-Content -LiteralPath $templatesStamp -Value $wantTemplatesStamp -NoNewline
    Write-Host "godot-fetch: installed export templates into $templatesDir"
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
