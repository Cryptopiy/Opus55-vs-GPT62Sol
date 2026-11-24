<#
  Runs the Unity editor in batch mode (GPU-free by default: -nographics) under run_guarded.ps1.
  Examples:
    .\unity_batch.ps1                                  # import + compile, then quit
    .\unity_batch.ps1 -Method LivingIsland.EditorTools.ProjectSetup.Run
    .\unity_batch.ps1 -Method LivingIsland.EditorTools.BuildTools.BuildPlayer -LogName build
#>
param(
    [string]$Method = "",
    [int]$TimeoutSec = 900,
    [string]$LogName = "batch",
    [switch]$Graphics,
    [switch]$NoQuit,
    [string]$Extra = ""
)
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$proj = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$log = Join-Path $proj "Logs\$LogName.log"
$a = "-batchmode -projectPath `"$proj`" -logFile `"$log`""
if (-not $Graphics) { $a += " -nographics" }
if ($Method) { $a += " -executeMethod $Method" }
if (-not $NoQuit) { $a += " -quit" }
if ($Extra) { $a += " $Extra" }
& (Join-Path $PSScriptRoot "run_guarded.ps1") -Exe $unity -Arguments $a -TimeoutSec $TimeoutSec -PollSec 15 -Tag "unity-$LogName"
$code = $LASTEXITCODE
Write-Output "---- log summary ($log)"
if (Test-Path $log) {
    Select-String -Path $log -Pattern "error CS\d+|Shader error|Compilation failed|\[LI\]|Build (succeeded|failed)|Exception|Error building|executeMethod" |
        Select-Object -First 80 | ForEach-Object { $_.Line }
}
exit $code
