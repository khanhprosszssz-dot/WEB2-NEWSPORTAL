$candidates = @()
$command = Get-Command dotnet -ErrorAction SilentlyContinue
if ($command) { $candidates += $command.Source }
foreach ($folder in @($env:ProgramW6432, $env:ProgramFiles)) {
    if ($folder) { $candidates += Join-Path $folder 'dotnet/dotnet.exe' }
}
foreach ($candidate in $candidates | Select-Object -Unique) {
    if ((Test-Path -LiteralPath $candidate) -and ((& $candidate --list-sdks) -match '^10\.')) { return $candidate }
}
throw '.NET 10 SDK was not found. Install the x64 SDK, then reopen PowerShell.'
