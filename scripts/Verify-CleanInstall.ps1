$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$runId = [Guid]::NewGuid().ToString('N')
# Keep NuGet resource paths below Windows' legacy MAX_PATH limit.
$runRoot = Join-Path $workspace ('.local/acceptance/' + $runId.Substring(0, 12))
$sourceRoot = Join-Path $runRoot 'source'
$databaseName = 'NewsPortal_Acceptance_' + $runId
$environmentNames = @('NUGET_PACKAGES','ConnectionStrings__DefaultConnection','Seed__Password','Jwt__Key','ASPNETCORE_ENVIRONMENT')
$originalEnvironment = @{}
foreach ($name in $environmentNames) { $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$originalRunning = $false
$processFile = Join-Path $workspace '.local/processes.json'
if (Test-Path $processFile) {
    foreach ($entry in (Get-Content $processFile -Raw | ConvertFrom-Json)) {
        $existing = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
        if ($existing -and $existing.Path -eq $entry.Path) { $originalRunning = $true }
    }
}
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
$report = [ordered]@{ RunId=$runId; StartedAt=[DateTime]::UtcNow.ToString('O'); Database=$databaseName; Source=$sourceRoot; Status='running'; Checks=@() }
$switched = $false
function Record-Check([string]$Name) {
    $report.Checks += $Name
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runRoot 'result.json') -Encoding UTF8
    Write-Host "PASS: $Name"
}
try {
    # Copy source and vendored web assets only. No binaries, secrets, uploads or prior reports.
    foreach ($folder in @('NewsPortal.Api','NewsPortal.Web','NewsPortal.Tests','scripts')) {
        $base = Join-Path $workspace $folder
        foreach ($file in Get-ChildItem -LiteralPath $base -File -Recurse) {
            $relative = $file.FullName.Substring($workspace.Length + 1)
            if ($relative -match '(^|\\)(bin|obj|uploads)(\\|$)') { continue }
            $target = Join-Path $sourceRoot $relative
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath $workspace -File) {
        if ($file.Extension -in @('.sln','.slnx','.md') -or $file.Name -in @('.gitignore','dotnet-tools.json')) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $sourceRoot $file.Name)
        }
    }
    if (Test-Path (Join-Path $sourceRoot '.local/development.json')) { throw 'Clean source unexpectedly contains credentials.' }
    Record-Check 'Source copied without bin/obj/uploads/local secrets'
    $configuration = Get-Content (Join-Path $sourceRoot 'NewsPortal.Api/appsettings.Development.json') -Raw | ConvertFrom-Json
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($configuration.ConnectionStrings.DefaultConnection)
    $builder['Initial Catalog'] = $databaseName
    $configuration.ConnectionStrings.DefaultConnection = $builder.ConnectionString
    $configuration | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $sourceRoot 'NewsPortal.Api/appsettings.Development.json') -Encoding UTF8
    $env:ConnectionStrings__DefaultConnection = $builder.ConnectionString
    $env:NUGET_PACKAGES = Join-Path $runRoot 'packages'
    $dotnetExe = & (Join-Path $sourceRoot 'scripts/Resolve-Dotnet.ps1')
    Set-Location $sourceRoot
    & $dotnetExe restore NewsPortal.sln
    if ($LASTEXITCODE -ne 0) { throw 'Clean NuGet restore failed.' }
    & $dotnetExe build NewsPortal.sln --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Clean build failed.' }
    Record-Check 'Restore with a new NuGet package directory and build from source'
    & (Join-Path $sourceRoot 'scripts/Initialize-Development.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Fresh migration/seed failed.' }
    # Re-running initialization must not duplicate seed records.
    & (Join-Path $sourceRoot 'scripts/Initialize-Development.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Repeated initialization failed.' }
    $connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    try {
        $connection.Open(); $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT (SELECT COUNT(*) FROM __EFMigrationsHistory) Migrations,(SELECT COUNT(*) FROM Categories) Categories,(SELECT COUNT(*) FROM Tags) Tags,(SELECT COUNT(*) FROM Articles) Articles,(SELECT COUNT(*) FROM AspNetRoles) Roles,(SELECT COUNT(*) FROM AspNetUsers) Users'
        $table = New-Object System.Data.DataTable; $table.Load($command.ExecuteReader()); $row=$table.Rows[0]
        if ($row.Migrations -ne 2 -or $row.Categories -ne 10 -or $row.Tags -ne 15 -or $row.Articles -ne 35 -or $row.Roles -ne 5 -or $row.Users -ne 5) { throw 'Fresh database or seed counts are incorrect.' }
        $report.Seed = @{Migrations=[int]$row.Migrations;Categories=[int]$row.Categories;Tags=[int]$row.Tags;Articles=[int]$row.Articles;Roles=[int]$row.Roles;Users=[int]$row.Users}
    } finally { $connection.Dispose() }
    Record-Check 'Fresh database migrations and idempotent seed: 10 categories, 15 tags, 35 articles, 5 roles/users'
    & (Join-Path $workspace 'scripts/Stop-Development.ps1')
    $switched = $true
    Set-Location $sourceRoot
    & (Join-Path $sourceRoot 'scripts/Verify-Published.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Clean published installation verification failed.' }
    Record-Check 'Fresh Release publish, automated API tests and full browser workflows'
    $report.Status = 'passed'
} catch {
    $report.Status = 'failed'
    $report.Error = $_.Exception.Message
    throw
} finally {
    if ($switched) { & (Join-Path $sourceRoot 'scripts/Stop-Development.ps1') }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name,$originalEnvironment[$name],'Process') }
    Set-Location $workspace
    if ($switched -and $originalRunning) { & (Join-Path $workspace 'scripts/Start-Development.ps1') -SkipBuild }
    $report.FinishedAt = [DateTime]::UtcNow.ToString('O')
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runRoot 'result.json') -Encoding UTF8
    Write-Host "Acceptance evidence: $runRoot"
    Write-Host "Isolated database target: $databaseName (retained if initialization completed)"
}
