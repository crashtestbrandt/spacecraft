<#
Verifies a .NET SDK the project can build with is on PATH: `dotnet` resolves and `dotnet --list-sdks`
lists an SDK of major version >= 8 (Spacecraft.csproj targets net8.0; a newer SDK still builds it).
Prints the SDK it found, or one readable line telling you to run `just dotnet-fetch`, and exits 1.

    just dotnet-check            print the resolved SDK (exit 1 if none)
    just build                   runs this first, so a missing SDK fails before MSBuild does

A runtime alone (`dotnet --list-runtimes`) is not enough: Godot's Build button and `dotnet build` both
need an SDK. This is the .NET counterpart of the Godot guard in tools/godot-run.ps1.
#>
[CmdletBinding()]
param(
    [int]$MinMajor = 8,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

# A fresh winget install lands on the machine PATH but not in the shell that ran it -- fold the registry
# PATH in so the check (and a build right after `just dotnet-fetch`) sees it without a new shell.
$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$userPath    = [Environment]::GetEnvironmentVariable("Path", "User")
$env:Path = ($env:Path, $machinePath, $userPath | Where-Object { $_ }) -join ";"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "dotnet is not on PATH. Run 'just dotnet-fetch' (installs the .NET $MinMajor SDK via winget) and try again."
    exit 1
}

$sdks = @(& $dotnet.Source --list-sdks 2>$null)
$ok = $sdks | ForEach-Object {
    if ($_ -match '^(\d+)\.(\d+)\.(\d+)') { [pscustomobject]@{ Major = [int]$Matches[1]; Line = $_ } }
} | Where-Object { $_.Major -ge $MinMajor } | Select-Object -First 1

if (-not $ok) {
    $have = if ($sdks.Count) { ($sdks -join "; ") } else { "none (runtime only?)" }
    Write-Error "No .NET SDK >= $MinMajor found. Installed SDKs: $have. Run 'just dotnet-fetch' and try again."
    exit 1
}

if (-not $Quiet) { Write-Host "dotnet-check: $($dotnet.Source) -- SDK $($ok.Line)" }
exit 0
