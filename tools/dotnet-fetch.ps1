<#
Installs the .NET 8 SDK through winget when tools/dotnet-check.ps1 finds no usable SDK. Unlike the Godot
editor (a pinned, gitignored download), the SDK is a machine-wide install: Godot's Build button only
finds the `dotnet` on PATH, so a project-local copy would need PATH plumbing to be usable from the editor.

    just dotnet-fetch            install the .NET 8 SDK unless a >= 8 SDK is already on PATH
    just dotnet-fetch -Force     run the winget install even when an SDK is already present

winget ships with Windows 10 21H2+ / Windows 11. The package id is pinned below; bump it when the
csproj's TargetFramework moves.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$PackageId = "Microsoft.DotNet.SDK.8"
$check = Join-Path $PSScriptRoot "dotnet-check.ps1"

if (-not $Force) {
    # The check script's Write-Error is terminating under its own Stop preference, so probe in a try.
    $present = $false
    try { & $check -Quiet 2>$null; $present = ($LASTEXITCODE -eq 0) } catch { $present = $false }
    if ($present) {
        & $check
        Write-Host "dotnet-fetch: SDK already present -- nothing to do (use -Force to reinstall)."
        exit 0
    }
}

$winget = Get-Command winget -ErrorAction SilentlyContinue
if (-not $winget) {
    Write-Error "dotnet-fetch: winget is not available. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and try again."
    exit 1
}

Write-Host "dotnet-fetch: winget install $PackageId"
& $winget.Source install --id $PackageId -e --accept-source-agreements --accept-package-agreements --disable-interactivity
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet-fetch: winget exited with $LASTEXITCODE"
    exit 1
}

# Re-verify; dotnet-check folds the machine PATH in so this works in the same shell.
& $check
exit $LASTEXITCODE
