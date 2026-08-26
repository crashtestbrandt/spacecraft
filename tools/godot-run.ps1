<#
Resolves the Godot editor via tools/godot-resolve.ps1 and runs it with the given arguments,
forwarding its exit code. Every justfile recipe that launches Godot goes through this rather than
naming a binary path directly, so "Godot isn't installed yet" is one readable message instead of
every recipe failing separately on an empty path.
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$GodotArgs
)

$ErrorActionPreference = "Stop"
$exe = & (Join-Path $PSScriptRoot "godot-resolve.ps1")
if (-not $exe) {
    Write-Error "Godot is not installed. Run 'just godot-fetch' and try again."
    exit 1
}

& $exe @GodotArgs
exit $LASTEXITCODE
