param(
    [ValidateSet('Both','Web','PC')][string]$Mode = 'Both',
    [int]$Port = 4174,
    [switch]$LAN,
    [string]$NodePath = $env:NODE_EXE,
    [string]$ServerRoot
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ServerRoot) {
    $ServerRoot = @((Join-Path (Split-Path $projectRoot) 'lumen'), (Join-Path (Split-Path $projectRoot) 'web')) | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'serve.mjs') } | Select-Object -First 1
}
if (-not $ServerRoot) { throw 'Nenašiel sa priečinok spoločného servera. Zadaj -ServerRoot.' }
$ServerRoot = [IO.Path]::GetFullPath($ServerRoot)
if (-not $NodePath) { $NodePath = 'node' }
$nodeCommand = Get-Command $NodePath -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $nodeCommand) { throw 'Nainštaluj Node.js 24 alebo novší, prípadne zadaj -NodePath.' }
$url = "http://127.0.0.1:$Port"
$serverReady = $false
try { $response = Invoke-WebRequest -Uri "$url/api/unity/unknown" -UseBasicParsing -TimeoutSec 2; $serverReady = $response.StatusCode -in 404,405 }
catch { if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -in 404,405) { $serverReady = $true } }
if (-not $serverReady) {
    $artifactRoot = Join-Path $projectRoot 'Artifacts'; New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    $previousPort = $env:PORT; $previousHost = $env:HOST
    try {
        $env:PORT = [string]$Port; $env:HOST = if ($LAN) { '0.0.0.0' } else { '127.0.0.1' }
        $serverProcess = Start-Process -FilePath $nodeCommand.Source -ArgumentList @('serve.mjs') -WorkingDirectory $ServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifactRoot 'local-server.log') -RedirectStandardError (Join-Path $artifactRoot 'local-server-errors.log')
        @{ProcessId=$serverProcess.Id; Port=$Port; LAN=[bool]$LAN; ServerRoot=$ServerRoot} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'local-server.json') -Encoding utf8
    } finally { $env:PORT = $previousPort; $env:HOST = $previousHost }
    for ($i=0; $i -lt 30; $i++) {
        try { $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2; if ($response.StatusCode -eq 200) { $serverReady=$true; break } } catch { }
        Start-Sleep -Milliseconds 200
    }
    if (-not $serverReady) { throw 'Server sa nespustil. Pozri Artifacts/local-server-errors.log.' }
}
if ($Mode -in 'PC','Both') {
    $player = Join-Path $projectRoot 'Builds/Windows/LumenAcademy.exe'
    if (-not (Test-Path -LiteralPath $player)) { throw 'Najprv zostav PC hru cez Tools/Build-Unity.ps1 -Action Build.' }
    Start-Process -FilePath $player -ArgumentList @('-lumen-online-origin',$url) -WindowStyle Normal
}
if ($Mode -in 'Web','Both') { Start-Process $url }
Write-Output "PC a web používajú rovnaký server: $url"
if ($LAN) {
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -match '^(192\.168\.|10\.|172\.(1[6-9]|2[0-9]|3[01])\.)' -and $_.AddressState -eq 'Preferred' } | ForEach-Object { Write-Output "Mobil v rovnakej sieti: http://$($_.IPAddress):$Port/" }
}
