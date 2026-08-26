<#
Headless self-test: one host process plus (Players - 1) guest processes, each with `autotest`. Every
process auto-readies, verifies the armies on its verified timeline at tick 120 (tick 240 with
-AutoPlay, after the scripted engagement) and exits 0 on success. This script waits for all of them
and exits nonzero if any failed or timed out. Logs land in the given -LogDir (default: a temp dir).
#>
param(
    [ValidateRange(2, 4)] [int] $Players = 2,
    [int] $Port = 7791,
    [int] $TimeoutSec = 90,
    [switch] $AutoPlay,
    [string] $LogDir = (Join-Path ([System.IO.Path]::GetTempPath()) "spacecraft-demo-test")
)

$ErrorActionPreference = "Stop"
$exe = & (Join-Path $PSScriptRoot "godot-resolve.ps1")
if (-not $exe) {
    Write-Error "Godot is not installed. Run 'just godot-fetch' and try again."
    exit 1
}
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
New-Item -ItemType Directory -Force $LogDir | Out-Null
Get-ChildItem $LogDir -Filter *.log | Remove-Item -Force   # stale logs would fake a verdict

$extra = @("autotest")
if ($AutoPlay) { $extra += "autoplay" }

function Launch([string[]] $userArgs, [string] $log) {
    $args = @("--headless", "--path", $project, "--") + $userArgs
    $p = Start-Process -FilePath $exe -ArgumentList $args -PassThru -NoNewWindow -RedirectStandardOutput $log
    # Windows PowerShell 5.1 leaves ExitCode null unless the handle was read before the process ends.
    $null = $p.Handle
    return $p
}

$procs = @()
$procs += Launch (@("host", "port=$Port", "players=$Players") + $extra) (Join-Path $LogDir "host.log")
Start-Sleep -Seconds 2
for ($i = 1; $i -lt $Players; $i++) {
    $procs += Launch (@("join", "ip=127.0.0.1", "port=$Port") + $extra) (Join-Path $LogDir "guest$i.log")
    Start-Sleep -Milliseconds 500
}

$deadline = (Get-Date).AddSeconds($TimeoutSec)
$failed = $false
foreach ($p in $procs) {
    $remaining = [int][Math]::Max(1, ($deadline - (Get-Date)).TotalMilliseconds)
    if (-not $p.WaitForExit($remaining)) {
        Write-Host "TIMEOUT pid=$($p.Id)"
        try { $p.Kill() } catch {}
        $failed = $true
        continue
    }
    Write-Host "pid=$($p.Id) exit=$($p.ExitCode)"
    if ($p.ExitCode -ne 0) { $failed = $true }
}

Get-ChildItem $LogDir -Filter *.log | ForEach-Object {
    $line = Select-String -Path $_.FullName -Pattern "=== SPACECRAFT DEMO" | Select-Object -First 1
    Write-Host ("{0}: {1}" -f $_.Name, ($(if ($line) { $line.Line } else { "(no verdict)" })))
}

if ($failed) { Write-Host "demo-test FAILED (logs: $LogDir)"; exit 1 }
Write-Host "demo-test OK ($Players players)"
