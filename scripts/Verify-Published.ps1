$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$dotnetExe = & (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
foreach ($item in @(@('NewsPortal.Api','api'),@('NewsPortal.Web','web'))) {
    & $dotnetExe publish ($item[0]+'/'+$item[0]+'.csproj') -c Release -o ('artifacts/'+$item[1])
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}
& (Join-Path $PSScriptRoot 'Stop-Development.ps1')
try {
    & (Join-Path $PSScriptRoot 'Start-Development.ps1') -Published
    & $dotnetExe test NewsPortal.sln --no-restore --logger 'trx;LogFileName=published.trx' --results-directory '.local/test-results'
    if ($LASTEXITCODE -ne 0) { throw 'Tests against published applications failed.' }
    & (Join-Path $PSScriptRoot 'Test-ReaderBrowser.ps1') -Full -Port 9230
    if ($LASTEXITCODE -ne 0) { throw 'Browser tests against published applications failed.' }
    Write-Host 'Published API/Web verified locally. Development configuration; no public deployment.'
} finally {
    & (Join-Path $PSScriptRoot 'Stop-Development.ps1')
    & (Join-Path $PSScriptRoot 'Start-Development.ps1') -SkipBuild
}
