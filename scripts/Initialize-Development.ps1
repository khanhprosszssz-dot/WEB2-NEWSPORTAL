$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$localDir = Join-Path (Get-Location) '.local'
New-Item -ItemType Directory -Path $localDir -Force | Out-Null
$configPath = Join-Path $localDir 'development.json'
if (!(Test-Path -LiteralPath $configPath)) {
    $bytes = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    @{ JwtKey = [Convert]::ToBase64String($bytes); Password = ('Np!' + [Guid]::NewGuid().ToString('N') + '9a') } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    $rng.Dispose()
}
$localConfig = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Seed__Password = $localConfig.Password
$env:Jwt__Key = $localConfig.JwtKey
$dotnetExe = & (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
& $dotnetExe run --project NewsPortal.Api --no-launch-profile -- --initialize
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
Write-Host 'Development database ready. Credentials: .local/development.json (ignored by Git).'
