param([switch]$SkipBuild, [switch]$Published)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (!(Test-Path '.local/development.json')) { throw 'Run Initialize-Development.ps1 first.' }
if (!$SkipBuild -and !$Published) {
    $dotnetExe = & (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
    & $dotnetExe build NewsPortal.sln --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$config = Get-Content '.local/development.json' -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Jwt__Key = $config.JwtKey
$env:Logging__LogLevel__Microsoft_EntityFrameworkCore = 'Warning'
$root = (Get-Location).Path
$pidsFile = Join-Path $root '.local/processes.json'
if (Test-Path $pidsFile) {
    $existing = Get-Content $pidsFile -Raw | ConvertFrom-Json
    foreach ($entry in $existing) {
        $running = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
        if ($running -and $running.Path -eq $entry.Path) { throw 'Development process already running. Run Stop-Development.ps1 first.' }
    }
}
$records = @()
foreach ($item in @(@('NewsPortal.Api',5100),@('NewsPortal.Web',5200))) {
    $dir = Join-Path $root $item[0]
    $exe = Join-Path $dir ('bin/Debug/net10.0/' + $item[0] + '.exe')
    if ($Published) {
        $folder = if ($item[0] -eq 'NewsPortal.Api') { 'api' } else { 'web' }
        $dir = Join-Path $root ('artifacts/' + $folder)
        $exe = Join-Path $dir ($item[0] + '.exe')
    }
    if (!(Test-Path -LiteralPath $exe)) { throw "Executable missing: $exe. Build or publish the application first." }
    $process = Start-Process -FilePath $exe -ArgumentList @('--urls',('http://localhost:' + $item[1])) -WorkingDirectory $dir -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root ('.local/' + $item[0] + '.log')) -RedirectStandardError (Join-Path $root ('.local/' + $item[0] + '.error.log'))
    $records += @{ Id = $process.Id; Path = $exe }
}
$records | ConvertTo-Json | Set-Content $pidsFile -Encoding UTF8
foreach ($address in @('http://localhost:5100/health','http://localhost:5200/')) {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $response = Invoke-WebRequest $address -UseBasicParsing -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch {}
        Start-Sleep -Milliseconds 300
    }
    if (!$ready) { throw "Application did not become ready at $address. Inspect .local logs; stop with Stop-Development.ps1." }
}
Write-Host 'Reader: http://localhost:5200 | Swagger: http://localhost:5100/swagger'
