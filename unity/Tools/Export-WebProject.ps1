param(
    [string]$SourcePath = (Split-Path -Parent $PSScriptRoot),
    [string]$DestinationPath = ""
)
$ErrorActionPreference = 'Stop'
$taskPython = Get-Command python -ErrorAction SilentlyContinue
if (-not $taskPython) { throw 'Python 3 is required to export the WebGL companion project.' }
$taskExportArgs = @((Join-Path $PSScriptRoot 'export_web_project.py'), '--source', $SourcePath)
if ($DestinationPath) { $taskExportArgs += @('--destination', $DestinationPath) }
& $taskPython.Source @taskExportArgs
if ($LASTEXITCODE -ne 0) { throw "Unity Web projection failed with exit code ${LASTEXITCODE}." }
