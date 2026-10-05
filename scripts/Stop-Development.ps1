$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$file = Join-Path $root '.local/processes.json'
# Find this checkout's executables even when the PID file is missing or stale.
# Never stop applications from another checkout based only on their name/PID.
$allowedPaths = foreach ($project in @('NewsPortal.Api', 'NewsPortal.Web')) {
    foreach ($configuration in @('Debug', 'Release')) {
        [IO.Path]::GetFullPath((Join-Path $root "$project/bin/$configuration/net10.0/$project.exe"))
    }
    $folder = if ($project -eq 'NewsPortal.Api') { 'api' } else { 'web' }
    [IO.Path]::GetFullPath((Join-Path $root "artifacts/$folder/$project.exe"))
}
$candidates = @(Get-Process -Name 'NewsPortal.Api', 'NewsPortal.Web' -ErrorAction SilentlyContinue)
if (Test-Path $file) {
    foreach ($entry in (Get-Content $file -Raw | ConvertFrom-Json)) {
        $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
        if ($process -and $process.Path -eq $entry.Path) { $candidates += $process }
    }
}
foreach ($process in ($candidates | Sort-Object Id -Unique)) {
    if (!$process.Path) {
        throw "Cannot verify executable path for PID $($process.Id). Run PowerShell with the same permissions as the application, then retry."
    }
    if ($allowedPaths -contains $process.Path) {
        Stop-Process -Id $process.Id -ErrorAction Stop
        if (!$process.WaitForExit(10000)) { throw "Application PID $($process.Id) did not stop." }
        Write-Host "Stopped $($process.ProcessName) (PID $($process.Id))."
    }
}
if (Test-Path $file) { Remove-Item -LiteralPath $file }
