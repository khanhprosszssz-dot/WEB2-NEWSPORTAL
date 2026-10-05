param([Parameter(Mandatory=$true)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace '.local/acceptance')) + [IO.Path]::DirectorySeparatorChar
if (!$SourceRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a source directory created by Verify-CleanInstall.ps1.' }
$output = Join-Path (Split-Path $SourceRoot -Parent) 'recovery'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$configuration = Get-Content (Join-Path $SourceRoot 'NewsPortal.Api/appsettings.Development.json') -Raw | ConvertFrom-Json
$connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($configuration.ConnectionStrings.DefaultConnection)
if ($connection['Initial Catalog'] -notmatch '^NewsPortal_Acceptance_[a-f0-9]{32}$') { throw 'The database must be an isolated acceptance database.' }
$secrets = Get-Content (Join-Path $SourceRoot '.local/development.json') -Raw | ConvertFrom-Json
$names=@('ASPNETCORE_ENVIRONMENT','Jwt__Key','ConnectionStrings__DefaultConnection','Api__BaseUrl','Cors__Origins__0')
$previous=@{}; foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$apiProcess=$null; $webProcess=$null
function Start-Api([string]$Log) {
    $dir=Join-Path $SourceRoot 'artifacts/api'
    return Start-Process (Join-Path $dir 'NewsPortal.Api.exe') -WorkingDirectory $dir -ArgumentList @('--urls','http://localhost:5300') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output ($Log+'.log')) -RedirectStandardError (Join-Path $output ($Log+'.error.log'))
}
function Ready([string]$Url) {
    for($attempt=0;$attempt -lt 30;$attempt++){
        try { if((Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200){return} } catch {}
        Start-Sleep -Milliseconds 200
    }
    throw "Service did not become ready: $Url"
}
function Dump-Page([string]$Name) {
    $chrome=Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
    $profile=Join-Path $output ('chrome-'+$Name+'-'+[Guid]::NewGuid().ToString('N'))
    $path=Join-Path $output ($Name+'.html')
    $browser=Start-Process $chrome -WindowStyle Hidden -PassThru -ArgumentList @('--headless','--disable-gpu','--no-first-run',('--user-data-dir="'+$profile+'"'),'--virtual-time-budget=8000','--dump-dom','http://localhost:5400/') -RedirectStandardOutput $path -RedirectStandardError (Join-Path $output ($Name+'.chrome.log'))
    if(!$browser.WaitForExit(20000)){Stop-Process -Id $browser.Id;throw 'Browser did not finish the recovery check.'}
    # Chrome on Windows can hand work to a child that still owns stdout after
    # the launcher exits. Wait for the complete DOM using a shared read handle.
    for($attempt=0;$attempt -lt 200;$attempt++){
        $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
        $reader=New-Object IO.StreamReader($stream)
        try{$html=$reader.ReadToEnd()}finally{$reader.Dispose()}
        if($html.Contains('</html>')){return $html}
        Start-Sleep -Milliseconds 100
    }
    throw 'Chrome did not produce a complete DOM.'
}
try {
    $env:ASPNETCORE_ENVIRONMENT='Development';$env:Jwt__Key=$secrets.JwtKey
    $env:ConnectionStrings__DefaultConnection=$configuration.ConnectionStrings.DefaultConnection
    $env:Api__BaseUrl='http://localhost:5300/';$env:Cors__Origins__0='http://localhost:5400'
    $apiProcess=Start-Api 'api-before';Ready 'http://localhost:5300/health'
    $dir=Join-Path $SourceRoot 'artifacts/web'
    $webProcess=Start-Process (Join-Path $dir 'NewsPortal.Web.exe') -WorkingDirectory $dir -ArgumentList @('--urls','http://localhost:5400') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'web.log') -RedirectStandardError (Join-Path $output 'web.error.log')
    Ready 'http://localhost:5400/'
    $allowedResponse=Invoke-WebRequest 'http://localhost:5300/api/v1/articles' -Method Options -UseBasicParsing -Headers @{Origin='http://localhost:5400';'Access-Control-Request-Method'='POST';'Access-Control-Request-Headers'='authorization,content-type'}
    if($allowedResponse.Headers['Access-Control-Allow-Origin'] -ne 'http://localhost:5400'){throw 'Configured CORS origin was not allowed.'}
    $deniedResponse=Invoke-WebRequest 'http://localhost:5300/api/v1/articles' -Method Options -UseBasicParsing -Headers @{Origin='https://untrusted.example';'Access-Control-Request-Method'='POST'}
    if($deniedResponse.Headers['Access-Control-Allow-Origin']){throw 'Unconfigured CORS origin was allowed.'}
    Write-Host 'PASS: CORS allows the configured origin and rejects an unrelated origin'
    Stop-Process -Id $apiProcess.Id
    if(!$apiProcess.WaitForExit(5000)){throw 'API did not stop.'}
    $failedPage=Dump-Page 'api-unavailable'
    if(!$failedPage.Contains('Không thể tải nội dung')){throw 'Reader did not display its handled API error state.'}
    if($failedPage.Contains($secrets.JwtKey) -or $failedPage.Contains($secrets.Password)){throw 'An error response exposed development secrets.'}
    Write-Host 'PASS: Reader displays an error state while the API is unavailable'
    $apiProcess=Start-Api 'api-after';Ready 'http://localhost:5300/health'
    $recoveredPage=Dump-Page 'api-recovered'
    if(!$recoveredPage.Contains('class="hero-grid"') -or $recoveredPage.Contains('Không thể tải nội dung')){throw 'Reader did not recover after the API restarted.'}
    Write-Host 'PASS: Reader loads database content again after API recovery'
    @{Status='passed';CheckedAt=[DateTime]::UtcNow.ToString('O');Checks=@('CORS origin allow/deny','Handled API outage without secret disclosure','Reader recovery after API restart')} | ConvertTo-Json | Set-Content (Join-Path $output 'result.json') -Encoding UTF8
} finally {
    foreach($process in @($apiProcess,$webProcess)){if($process -and !$process.HasExited){Stop-Process -Id $process.Id -ErrorAction SilentlyContinue}}
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
}
