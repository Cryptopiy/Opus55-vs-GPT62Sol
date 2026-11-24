<#
  Runs the editor Play Mode smoke test (Assets/Tests/PlayMode) in batch mode WITH graphics (the game uses
  compute shaders), under run_guarded.ps1 (GPU gate, monitoring, PID-based timeout).
#>
param([int]$TimeoutSec = 900)
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$proj = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$log = Join-Path $proj "Logs\playmode.log"
$xml = Join-Path $proj "Logs\playmode_results.xml"
if (Test-Path $xml) { Remove-Item $xml }
$a = "-batchmode -projectPath `"$proj`" -runTests -testPlatform PlayMode -testResults `"$xml`" -logFile `"$log`""
& (Join-Path $PSScriptRoot "run_guarded.ps1") -Exe $unity -Arguments $a -TimeoutSec $TimeoutSec -PollSec 10 -Tag "playmode"
$code = $LASTEXITCODE
Write-Output "---- play mode test ($xml)"
if (Test-Path $xml) {
    [xml]$r = Get-Content $xml
    $run = $r.'test-run'
    Write-Output ("result={0} passed={1} failed={2} duration={3}s" -f $run.result, $run.passed, $run.failed, $run.duration)
}
if (Test-Path $log) {
    Select-String -Path $log -Pattern "\[LI\] PLAYMODE|\[LI\] Island|\[LI\] Startup|error CS" | ForEach-Object { $_.Line }
}
exit $code
