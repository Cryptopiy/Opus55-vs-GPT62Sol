<#
  Runs the built Windows player (Build/LivingIsland.exe, or <BuildDir>/LivingIsland.exe) windowed under run_guarded.ps1.
  Example: .\run_player.ps1 -GameArgs "-autotest acceptance" -TimeoutSec 115 -Width 1920 -Height 1080
#>
param(
    [string]$GameArgs = "",
    [int]$TimeoutSec = 115,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$LogName = "player",
    [string]$BuildDir = "Build"
)
$proj = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$exe = Join-Path $proj "$BuildDir\LivingIsland.exe"
$log = Join-Path $proj "Logs\$LogName.log"
$a = "-screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$log`" $GameArgs"
& (Join-Path $PSScriptRoot "run_guarded.ps1") -Exe $exe -Arguments $a -TimeoutSec $TimeoutSec -PollSec 5 -Tag $LogName
$code = $LASTEXITCODE
Write-Output "---- player log summary ($log)"
if (Test-Path $log) {
    Select-String -Path $log -Pattern "\[LI\]|Exception|error|warning|invalid|failed|not supported|missing" -CaseSensitive:$false |
        Where-Object { $_.Line -notmatch "^\s*$" } | Select-Object -First 160 | ForEach-Object { $_.Line }
    # Native engine messages (not routed through Application.logMessageReceived) are listed separately.
    $native = Select-String -Path $log -Pattern "warning|error|invalid|failed|not supported" -CaseSensitive:$false |
        Where-Object { $_.Line -notmatch "\[LI\]" }
    Write-Output ("---- non-[LI] warning/error-like lines: {0}" -f @($native).Count)
    $native | Select-Object -First 40 | ForEach-Object { "  " + $_.Line }
}
exit $code
