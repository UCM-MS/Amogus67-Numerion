param([string]$UnityPath, [switch]$SkipCopy)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
$editorVersion = ($versionLine -split ':',2)[1].Trim()
if (-not $UnityPath) {
    $UnityPath = @("$env:ProgramFiles/Unity/Hub/Editor/$editorVersion/Editor/Unity.exe", "$env:LOCALAPPDATA/UnityEditors/$editorVersion/Editor/Unity.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) { throw "Nainštaluj Unity $editorVersion s Web Build Support alebo zadaj -UnityPath." }
& python (Join-Path $PSScriptRoot 'export_web_project.py')
if ($LASTEXITCODE -ne 0) { throw 'Príprava webového projektu sa nepodarila.' }
$webProject = Join-Path (Split-Path $projectRoot) 'lumen-unity-web'
$artifacts = Join-Path $webProject 'Artifacts'; New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$log = Join-Path $artifacts 'build-6.6.log'
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$webProject+'"'),'-buildTarget','WebGL','-executeMethod','Lumen.Editor.LumenWebBuilder.Build','-logFile',('"'+$log+'"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Unity may leave a console helper alive; wait for the editor, not the whole child-process job.
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Web sa nepodarilo zostaviť. Podrobnosti: $log" }
$output = Join-Path $webProject 'Builds/WebGL'
if (-not $SkipCopy) {
    $server = @((Join-Path (Split-Path $projectRoot) 'lumen'), (Join-Path (Split-Path $projectRoot) 'web')) | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'serve.mjs') } | Select-Object -First 1
    if ($server) {
        $destination = Join-Path $server 'public/unity'; New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Get-ChildItem -LiteralPath $output | Copy-Item -Destination $destination -Recurse -Force
        $prepareHosting = Join-Path $server 'scripts/prepare-unity-hosting.mjs'
        if (Test-Path -LiteralPath $prepareHosting) {
            $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
            $hostingNode = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
            if (-not (Test-Path -LiteralPath $hostingNode)) { throw 'Na prípravu webových dát nainštaluj Node.js 24 alebo novší.' }
            Push-Location -LiteralPath $server
            try { & $hostingNode $prepareHosting; if ($LASTEXITCODE -ne 0) { throw 'Webové dátové časti sa nepodarilo pripraviť.' } }
            finally { Pop-Location }
        }
        Write-Output "Unity Web skopírované do lokálneho servera: $destination"
    }
}
Write-Output "Unity Web zostavené: $output"
