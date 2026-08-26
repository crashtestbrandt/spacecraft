<#
Launches a local multi-instance demo: one host window plus (Players - 1) guest windows, tiled so they
do not overlap, all on 127.0.0.1:Port. Every instance auto-readies when -AutoPlay is given, which also
scripts a full-army charge to the arena center so the match plays itself.

Windows are 640x360 (the game's base resolution, 1:1 pixels). Pass -Players 2..4.
#>
param(
    [ValidateRange(2, 4)] [int] $Players = 2,
    [int] $Port = 7777,
    [switch] $AutoPlay
)

$ErrorActionPreference = "Stop"
$exe = & (Join-Path $PSScriptRoot "godot-resolve.ps1")
if (-not $exe) {
    Write-Error "Godot is not installed. Run 'just godot-fetch' and try again."
    exit 1
}
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$positions = @("40,60", "720,60", "40,480", "720,480")

$common = @("--path", $project, "--resolution", "640x360")
$extra = @()
if ($AutoPlay) { $extra = @("autotest", "autoplay") }

# Host first so the guests find a listener; --position keeps the windows tiled.
Start-Process -FilePath $exe -ArgumentList ($common + @("--position", $positions[0], "--", "host", "port=$Port", "players=$Players") + $extra)
Start-Sleep -Seconds 2
for ($i = 1; $i -lt $Players; $i++) {
    Start-Process -FilePath $exe -ArgumentList ($common + @("--position", $positions[$i], "--", "join", "ip=127.0.0.1", "port=$Port") + $extra)
    Start-Sleep -Milliseconds 500
}
Write-Host "Launched $Players instances on 127.0.0.1:$Port. Click Ready in each window (or pass -AutoPlay)."
