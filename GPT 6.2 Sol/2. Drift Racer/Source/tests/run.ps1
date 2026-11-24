param(
    [Parameter(Mandatory=$true)][string]$Name,
    [string[]]$EngineArgs = @('--headless', '--quit-after', '120'),
    [ValidateRange(1,1800)][int]$Limit = 120,
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workspace = Split-Path -Parent $project
$logs = Join-Path $workspace '.test-logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$engine = 'Godot_v4.7.2-stable_win64.exe'
$logFile = Join-Path $logs "$Name.log"
$telemetryFile = Join-Path $logs "$Name.resources.log"

function Read-Resources {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command nvidia-smi.exe).Source
    $start.Arguments = '--query-gpu=memory.used,memory.total,utilization.gpu,temperature.gpu,power.draw --format=csv,noheader'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $probe = [System.Diagnostics.Process]::Start($start)
    try {
        if (-not $probe.WaitForExit(2000)) {
            $probe.Kill()
            throw 'nvidia-smi timed out'
        }
        if ($probe.ExitCode -ne 0) { throw 'nvidia-smi failed' }
        $raw = $probe.StandardOutput.ReadToEnd().Trim()
    } finally { $probe.Dispose() }
    $vram, $total, $util, $temp, $draw = $raw -split ',\s*'
    if ($vram -notmatch '^\d+' -or $temp -notmatch '^\d+') { throw "Invalid GPU probe: $raw" }
    $free = (Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 2).FreePhysicalMemory
    if ($null -eq $free) { throw 'Free RAM unavailable' }
    $sample = "$(Get-Date -Format HH:mm:ss) GPU: $raw; Free RAM: $free KB"
    Write-Host $sample
    Add-Content -LiteralPath $telemetryFile -Value $sample -Encoding UTF8
    [pscustomobject]@{ Vram = [int]($vram -replace '\D'); Temperature = [int]($temp -replace '\D'); Free = $free }
}

if ((Get-PSDrive -Name $workspace.Substring(0,1)).Free -lt 20GB) { throw 'Less than 20 GB free on the project drive' }
if ($Graphics) {
    $safe = $false
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        $resources = Read-Resources
        if ($resources.Vram -le 8704 -and $resources.Temperature -le 78 -and $resources.Free -ge 12582912) {
            $safe = $true
            break
        }
        if ($attempt -lt 9) { Start-Sleep -Seconds (Get-Random -Minimum 30 -Maximum 91) }
    }
    if (-not $safe) { throw 'GPU launch gate remained closed after ten checks' }
    if ($Limit -gt 120) { throw 'Graphics tests must stop within 120 seconds' }
}
$argsForEngine = @('--path', ('"{0}"' -f $project), '--log-file', ('"{0}"' -f $logFile)) + $EngineArgs
$launch = [System.Diagnostics.ProcessStartInfo]::new()
$launch.FileName = $engine
$launch.Arguments = $argsForEngine -join ' '
$launch.WorkingDirectory = $project
$launch.UseShellExecute = $false
$launch.RedirectStandardOutput = $true
$launch.RedirectStandardError = $true
$launch.EnvironmentVariables['APPDATA'] = $logs
$launch.EnvironmentVariables['LOCALAPPDATA'] = $logs
$process = [System.Diagnostics.Process]::Start($launch)
$standardOutput = $process.StandardOutput.ReadToEndAsync()
$standardError = $process.StandardError.ReadToEndAsync()
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$nextSample = 15.0
Write-Host "Godot test '$Name' running as PID $($process.Id)"
try {
    while (-not $process.WaitForExit(180)) {
        if ($watch.Elapsed.TotalSeconds -ge $Limit) { throw "PID $($process.Id) hit the external time limit" }
        if ($Graphics -and $watch.Elapsed.TotalSeconds -ge $nextSample) {
            $nextSample += 15.0
            $resources = Read-Resources
            if ($resources.Vram -gt 10752 -or $resources.Temperature -ge 83 -or $resources.Free -lt 6291456) {
                throw "Hard stop triggered for PID $($process.Id)"
            }
        }
    }
    $result = $process.ExitCode
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $consoleText = $standardOutput.GetAwaiter().GetResult() + $standardError.GetAwaiter().GetResult()
    if ($consoleText) {
        $consoleText | Set-Content -LiteralPath (Join-Path $logs "$Name.console.log") -Encoding UTF8
        Write-Host $consoleText.TrimEnd()
    }
    $process.Dispose()
    if (-not $consoleText -and (Test-Path -LiteralPath $logFile)) { Get-Content -LiteralPath $logFile }
}
exit $result
