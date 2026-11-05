<#
.SYNOPSIS
Starts one copy of Slay the Spire 2 for a local co-op test on one PC: the host or the client,
outside Steam, in a window, with its own log file.

.DESCRIPTION
Start the host first and wait for its co-op character select, then start the client; the client
joins the host at 127.0.0.1. Run from the repository root:

  pwsh -NoProfile -File scripts/launch-coop.ps1 -Role Host
  pwsh -NoProfile -File scripts/launch-coop.ps1 -Role Client

The game switches used: --force-steam off (skip Steam; the players are "Test Host" and
"Test Client 1"), --fastmp host_standard or join (host or join over ENet on port 33771),
--clientId 1000 (the client's player id), -wpos X Y (windowed at that offset), --nomods, and
Godot's --log-file. Each player id has its own profile under %APPDATA%\SlayTheSpire2\default\<id>,
apart from your Steam saves. The logs are coop-host.log and coop-client.log in
%APPDATA%\SlayTheSpire2\logs.

.PARAMETER Role
Host or Client.

.PARAMETER NoMods
Starts the client without mods, as a teammate who does not have the mod.

.PARAMETER GameDir
The Slay the Spire 2 folder; found through the Steam registry when omitted.
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Host', 'Client')] [string] $Role,
    [switch] $NoMods,
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'

if (-not $GameDir) {
    $steam = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($steam -and $steam.SteamPath) {
        $GameDir = Join-Path $steam.SteamPath 'steamapps\common\Slay the Spire 2'
    }
}

$exe = if ($GameDir) { Join-Path $GameDir 'SlayTheSpire2.exe' }
if (-not $exe -or -not (Test-Path $exe)) { throw 'SlayTheSpire2.exe not found from the Steam registry; pass -GameDir with the Slay the Spire 2 folder.' }

$logs = Join-Path $env:APPDATA 'SlayTheSpire2\logs'
$running = @(Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue)

if ($Role -eq 'Host') {
    if ($running.Count -gt 0) { throw "The game is already running ($($running.Count) process); close it before starting the host." }
    $log = Join-Path $logs 'coop-host.log'
    $arguments = @('--force-steam', 'off', '--fastmp', 'host_standard', '-wpos', '0', '0', '--log-file', $log)
}
else {
    if ($running.Count -ne 1) { throw "Expected exactly one running copy (the host), found $($running.Count)." }
    $log = Join-Path $logs 'coop-client.log'
    $arguments = @('--force-steam', 'off', '--fastmp', 'join', '--clientId', '1000', '-wpos', '120', '80', '--log-file', $log)
    if ($NoMods) { $arguments += '--nomods' }
}

$dll = Join-Path $GameDir 'mods\WaitIHaveVuln\WaitIHaveVuln.dll'
$hash = if (Test-Path $dll) { (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant() } else { 'not deployed' }
"role: $Role"
"exe: $exe"
"arguments: $($arguments -join ' ')"
"log: $log"
"deployed sha256: $hash"
"started at: $((Get-Date).ToString('o'))"
Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $GameDir | Out-Null
