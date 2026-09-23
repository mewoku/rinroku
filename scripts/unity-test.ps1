param([ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode', [string]$Category = '')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $workspace 'artifacts/logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$unity = 'D:\6000.6.0f1\Editor\Unity.exe'

$arguments = @(
    '-batchmode',
    '-projectPath', (Join-Path $workspace 'client'),
    '-runTests', '-testPlatform', $Platform,
    '-testResults', (Join-Path $logs "$Platform-results.xml"),
    '-logFile', (Join-Path $logs "unity-$Platform.log")
)
if ($Category) { $arguments += @('-testCategory', $Category) } else { $arguments += @('-testCategory', '!Capture') }
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
exit $process.ExitCode
