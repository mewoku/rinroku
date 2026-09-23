$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$unity = 'D:\6000.6.0f1\Editor\Unity.exe'

$arguments = @(
    '-batchmode', '-quit',
    '-projectPath', (Join-Path $workspace 'client'),
    '-executeMethod', 'Ronriku.Editor.RonrikuBuild.BuildAndroid',
    '-logFile', (Join-Path $workspace 'unity-android.log')
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
exit $process.ExitCode
