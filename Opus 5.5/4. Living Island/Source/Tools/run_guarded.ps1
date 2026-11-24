<#
  Runs one process under the shared-PC GPU safety rules (MASTER_PROMPT section 5):
  - launch gate: VRAM used <= 8.5 GB, GPU temp <= 78 C, free RAM >= 12 GB (else wait 30-90 s, max 10 checks)
  - monitoring every PollSec seconds (<= 15 s) with a CSV log of every sample
  - hard stop: VRAM > 10.5 GB, temp >= 83 C, free RAM < 6 GB, or nvidia-smi failing -> kill our process tree
  - hard timeout -> kill our process tree (by PID only, never by name)
  Exit codes: process exit code, 3 = gate failed, 4 = hard stop, 5 = timeout.
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Arguments = "",
    [int]$TimeoutSec = 120,
    [int]$PollSec = 5,
    [string]$Tag = "run",
    [switch]$SkipGate
)

$ErrorActionPreference = "Continue"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$csv = Join-Path $projectRoot "Logs\gpu_monitor.csv"
if (-not (Test-Path (Split-Path $csv))) { New-Item -ItemType Directory -Force (Split-Path $csv) | Out-Null }
if (-not (Test-Path $csv)) { "time,tag,vram_mb,util,temp_c,power_w,free_ram_gb" | Out-File -FilePath $csv -Encoding ascii }

function Invoke-Smi {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "nvidia-smi"
    $psi.Arguments = "--query-gpu=memory.used,memory.total,utilization.gpu,temperature.gpu,power.draw --format=csv,noheader,nounits"
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    try { $p = [System.Diagnostics.Process]::Start($psi) } catch { return $null }
    if (-not $p.WaitForExit(10000)) { try { $p.Kill() } catch {}; return $null }
    return $p.StandardOutput.ReadToEnd().Trim()
}

function Get-Stats {
    $line = Invoke-Smi
    if (-not $line) { return $null }
    $parts = $line.Split(',') | ForEach-Object { $_.Trim() }
    $freeKB = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory
    $power = 0.0
    [double]::TryParse($parts[4], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$power) | Out-Null
    return [pscustomobject]@{
        VramMB    = [double]$parts[0]
        Util      = [double]$parts[2]
        TempC     = [double]$parts[3]
        PowerW    = $power
        FreeRamGB = [math]::Round($freeKB / 1MB, 2)
    }
}

function Write-Sample($s, $tag) {
    $t = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    ("{0},{1},{2},{3},{4},{5},{6}" -f $t, $tag, $s.VramMB, $s.Util, $s.TempC, $s.PowerW.ToString($inv), $s.FreeRamGB.ToString($inv)) | Out-File -FilePath $csv -Append -Encoding ascii
}

function Stop-Tree($proc) {
    if ($proc -and -not $proc.HasExited) {
        & taskkill /PID $proc.Id /T /F | Out-Null
    }
}

# ---------------------------------------------------------------- launch gate
$fails = 0
while (-not $SkipGate) {
    $s = Get-Stats
    if ($s) { Write-Sample $s "$Tag-gate" }
    if ($s -and $s.VramMB -le 8704 -and $s.TempC -le 78 -and $s.FreeRamGB -ge 12) {
        Write-Output ("GATE OK: VRAM {0} MB, {1} C, free RAM {2} GB" -f $s.VramMB, $s.TempC, $s.FreeRamGB)
        break
    }
    $fails++
    if ($fails -ge 10) { Write-Output "GATE FAILED 10 times - do GPU-free work and retry later"; exit 3 }
    $wait = Get-Random -Minimum 30 -Maximum 91
    Write-Output ("Gate not met ({0}); waiting {1} s" -f ($s | Out-String).Trim(), $wait)
    Start-Sleep -Seconds $wait
}

# ---------------------------------------------------------------- run + monitor
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $Exe -ArgumentList $Arguments -PassThru
Write-Output ("STARTED PID {0}: {1}" -f $proc.Id, $Exe)
$peakVram = 0; $peakTemp = 0; $minRam = 999; $result = $null
while (-not $proc.HasExited) {
    $slept = 0
    while ($slept -lt $PollSec -and -not $proc.HasExited) { Start-Sleep -Milliseconds 500; $slept += 0.5 }
    if ($proc.HasExited) { break }
    $s = Get-Stats
    if (-not $s) {
        Write-Output "nvidia-smi failed/hung -> overload sign, stopping our process"
        Stop-Tree $proc; $result = 4; break
    }
    Write-Sample $s $Tag
    if ($s.VramMB -gt $peakVram) { $peakVram = $s.VramMB }
    if ($s.TempC -gt $peakTemp) { $peakTemp = $s.TempC }
    if ($s.FreeRamGB -lt $minRam) { $minRam = $s.FreeRamGB }
    if ($s.VramMB -gt 10752 -or $s.TempC -ge 83 -or $s.FreeRamGB -lt 6) {
        Write-Output ("HARD STOP: VRAM {0} MB, {1} C, free RAM {2} GB" -f $s.VramMB, $s.TempC, $s.FreeRamGB)
        Stop-Tree $proc; $result = 4; break
    }
    if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
        Write-Output ("TIMEOUT after {0:N0} s -> killing PID {1}" -f $sw.Elapsed.TotalSeconds, $proc.Id)
        Stop-Tree $proc; $result = 5; break
    }
}
if ($null -eq $result) { $proc.WaitForExit(); $result = $proc.ExitCode }
Write-Output ("FINISHED in {0:N0} s, exit {1}; peak VRAM {2} MB, peak temp {3} C, min free RAM {4} GB" -f $sw.Elapsed.TotalSeconds, $result, $peakVram, $peakTemp, $minRam)
exit $result
