$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $workspace 'artifacts/logs') | Out-Null
$unity = 'D:\6000.6.0f1\Editor\Unity.exe'

$arguments = @(
    '-batchmode', '-quit',
    '-projectPath', (Join-Path $workspace 'client'),
    '-executeMethod', 'Ronriku.Editor.RonrikuBuild.BuildAndroid',
    '-logFile', (Join-Path $workspace 'artifacts/logs/unity-android.log')
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
exit $process.ExitCode
