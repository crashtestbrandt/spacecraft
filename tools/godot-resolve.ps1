<#
Resolves the Godot 4.7 .NET (mono) editor binary this project's `just` recipes run.

GODOT_BIN wins outright -- point it at an existing Godot 4.7 mono install to skip the fetch/pin
machinery in tools/godot-fetch.ps1 entirely. Otherwise this looks in .godot-editor/ (or
$env:SPACECRAFT_GODOT_HOME, for an editor kept somewhere else), where `just godot-fetch` installs
the pinned release. Prints nothing if neither resolves -- the `godot-guard` justfile recipe is what
turns that empty string into a readable error rather than a bad path.

GODOT_BIN IS NOT TRUSTED BLINDLY. It is an ordinary per-machine env var, so a box already set up for
some OTHER Godot project (a plain, non-.NET editor for a GDScript-only project, say) can leave it
pointing at a binary that will silently fail to load this project's C# -- every official Godot .NET
build's filename carries "mono", so that is what is checked before trusting it.
#>

if ($env:GODOT_BIN -and (Test-Path -LiteralPath $env:GODOT_BIN -PathType Leaf)) {
    if ((Split-Path -Leaf $env:GODOT_BIN) -match "(?i)mono") {
        (Resolve-Path -LiteralPath $env:GODOT_BIN).Path
        exit 0
    }
    Write-Warning "GODOT_BIN ($env:GODOT_BIN) does not look like a Godot .NET (mono) build -- ignoring it and falling back to .godot-editor/."
}

$installDir = if ($env:SPACECRAFT_GODOT_HOME) { $env:SPACECRAFT_GODOT_HOME } else { Join-Path $PSScriptRoot "..\.godot-editor" }
if (-not (Test-Path -LiteralPath $installDir)) { exit 0 }

$exe = Get-ChildItem -LiteralPath $installDir -Recurse -Filter "Godot_*.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notlike "*console*" } |
    Select-Object -First 1
if ($exe) { $exe.FullName }
