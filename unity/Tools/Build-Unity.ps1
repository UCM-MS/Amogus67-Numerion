param(
    [ValidateSet('Prepare','Validate','Build','Test','Open')][string]$Action = 'Build',
    [string]$UnityPath = $env:UNITY_EDITOR_PATH
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
$editorVersion = ($versionLine -replace '^m_EditorVersion: ', '').Trim()
if (-not $editorVersion) { throw "Chýba verzia Unity v $versionFile." }
if (-not $UnityPath) {
    $candidates = @(
        "$env:LOCALAPPDATA/UnityEditors/$editorVersion/Editor/Unity.exe",
        "$env:ProgramFiles/Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
    )
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor $editorVersion sa nenašiel. Zadaj -UnityPath s cestou k Unity.exe."
}
if ($Action -eq 'Open') {
    Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', ('"'+$projectRoot+'"')) -WindowStyle Normal
    exit 0
}
$artifactRoot = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
$log = Join-Path $artifactRoot ($Action.ToLowerInvariant()+'.log')
$arguments = @('-batchmode','-nographics','-projectPath',('"'+$projectRoot+'"'),'-logFile',('"'+$log+'"'))
if ($Action -eq 'Test') {
    $arguments += @('-runTests','-testPlatform','EditMode','-testFilter','Lumen.Tests','-testResults',('"'+(Join-Path $artifactRoot 'editmode-results.xml')+'"'))
} else {
    $method = @{Prepare='Prepare';Validate='Validate';Build='BuildWindows'}[$Action]
    $arguments += @('-quit','-executeMethod',('Lumen.Editor.LumenProjectBuilder.'+$method))
}
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity skončilo s kódom $($process.ExitCode). Diagnostika: $log" }
Write-Output "Unity ${Action}: $log"
