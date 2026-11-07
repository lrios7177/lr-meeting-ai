$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$installer = Get-ChildItem (Join-Path $root 'artifacts/installer/*.exe') | Select-Object -First 1
if (!$installer) { throw 'Installer not found.' }
$installDir = Join-Path $env:RUNNER_TEMP 'LR Meeting AI smoke'
$install = Start-Process $installer.FullName -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $installDir + '"')) -Wait -PassThru
if ($install.ExitCode -ne 0) { throw "Installer exited with code $($install.ExitCode)." }
$snapshot = Join-Path $root 'artifacts/smoke-meeting.png'
$app = Start-Process (Join-Path $installDir 'LRMeetingAI.exe') -ArgumentList @('--smoke-test',('"' + $snapshot + '"')) -PassThru
try {
    if (!$app.WaitForExit(90000)) { throw 'Windows UI smoke test exceeded 90 seconds.' }
    if ($app.ExitCode -ne 0) { throw "Windows UI smoke test failed with code $($app.ExitCode)." }
    if (!(Test-Path $snapshot)) { throw 'UI did not render a snapshot.' }
    if (!(Test-Path ([System.IO.Path]::ChangeExtension($snapshot, 'checks.txt')))) { throw 'Audio conversion checks did not finish.' }
} finally {
    if (!$app.HasExited) { Stop-Process -Id $app.Id -Force }
    if (Test-Path (Join-Path $installDir 'unins000.exe')) {
        $uninstall = Start-Process (Join-Path $installDir 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw 'Uninstall smoke check failed.' }
    }
}
