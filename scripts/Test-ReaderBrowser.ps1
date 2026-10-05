param([string]$BaseUrl = 'http://localhost:5200', [int]$Port = 9223, [switch]$Full)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root '.local/browser-qa'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$chrome = Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
if (!(Test-Path $chrome)) { throw 'Google Chrome is required.' }
$profile = Join-Path $output ('profile-' + [Guid]::NewGuid().ToString('N'))
$process = Start-Process $chrome -WindowStyle Hidden -PassThru -ArgumentList @('--headless', '--disable-gpu', '--no-first-run', "--remote-debugging-port=$Port", ('--user-data-dir="' + $profile + '"'), 'about:blank')
$socket = New-Object System.Net.WebSockets.ClientWebSocket
$script:commandId = 0
$script:exceptions = @()
function Invoke-Cdp([string]$Method, $Parameters = @{}) {
    $script:commandId++
    $id = $script:commandId
    $json = @{ id = $id; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 15 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $timeout = New-Object System.Threading.CancellationTokenSource
    $timeout.CancelAfter(20000)
    try {
        $null = $socket.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult()
        do {
            $stream = New-Object IO.MemoryStream
            try {
                do {
                    $buffer = New-Object byte[] 65536
                    $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
                    if ($received.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) { throw 'Chrome disconnected.' }
                    $stream.Write($buffer, 0, $received.Count)
                } while (!$received.EndOfMessage)
                $message = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
            } finally { $stream.Dispose() }
            if ($message.method -eq 'Runtime.exceptionThrown') { $script:exceptions += $message.params.exceptionDetails }
            if ($message.method -eq 'Runtime.consoleAPICalled' -and $message.params.type -eq 'error') { $script:exceptions += $message.params }
        } while ($message.id -ne $id)
        if ($message.error) { throw ($message.error | ConvertTo-Json -Compress) }
        return $message.result
    } finally { $timeout.Dispose() }
}
try {
    $tabs = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $tabs = Invoke-RestMethod "http://localhost:$Port/json"; break } catch { Start-Sleep -Milliseconds 200 }
    }
    if (!$tabs) { throw 'Chrome debugging endpoint did not start.' }
    $tab = $tabs | Where-Object { $_.type -eq 'page' -and $_.url -eq 'about:blank' } | Select-Object -First 1
    if (!$tab) { throw 'Chrome did not create the test page.' }
    $null = $socket.ConnectAsync([Uri]$tab.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Invoke-Cdp 'Page.enable' | Out-Null
    Invoke-Cdp 'Runtime.enable' | Out-Null
    $reports = @()
    foreach ($width in @(1440, 390)) {
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{ width = $width; height = 900; deviceScaleFactor = 1; mobile = ($width -eq 390) } | Out-Null
        foreach ($route in @(@('home', '/'), @('search', '/tim-kiem'), @('login', '/dang-nhap'), @('community', '/cong-dong'))) {
            $script:exceptions = @()
            Invoke-Cdp 'Page.navigate' @{ url = $BaseUrl + $route[1] } | Out-Null
            $ready = $false
            for ($attempt = 0; $attempt -lt 40; $attempt++) {
                Start-Sleep -Milliseconds 250
                $state = Invoke-Cdp 'Runtime.evaluate' @{ expression = "document.readyState === 'complete' && !!document.querySelector('#app h1, #app .hero, #app .empty')"; returnByValue = $true }
                if ($state.result.value) { $ready = $true; break }
            }
            if (!$ready) {
                $diagnostic = Invoke-Cdp 'Runtime.evaluate' @{ expression = 'JSON.stringify({url:location.href,html:document.body?.innerHTML})'; returnByValue = $true }
                $diagnostic | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'failure.json') -Encoding UTF8
                throw "Page did not render: $($route[1]). See .local/browser-qa/failure.json."
            }
            Start-Sleep -Milliseconds 500
            $metrics = Invoke-Cdp 'Runtime.evaluate' @{ expression = "JSON.stringify({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,title:document.title,failed:document.querySelector('#app')?.textContent.includes('Kh\u00f4ng th\u1ec3 t\u1ea3i n\u1ed9i dung'),brokenImages:[...document.images].filter(i=>i.complete&&i.naturalWidth===0).length})"; returnByValue = $true }
            $data = $metrics.result.value | ConvertFrom-Json
            $shot = Invoke-Cdp 'Page.captureScreenshot' @{ format = 'png'; captureBeyondViewport = $false }
            [IO.File]::WriteAllBytes((Join-Path $output ($route[0] + '-' + $width + '.png')), [Convert]::FromBase64String($shot.data))
            $reports += [pscustomobject]@{ Route = $route[1]; Width = $data.width; ScrollWidth = $data.scrollWidth; Failed = $data.failed; BrokenImages = $data.brokenImages; Exceptions = @($script:exceptions).Count }
        }
    }
    $reports | ConvertTo-Json | Set-Content (Join-Path $output 'report.json') -Encoding UTF8
    $reports | Format-Table
    if ($reports | Where-Object { $_.Failed -or $_.BrokenImages -gt 0 -or $_.Exceptions -gt 0 -or $_.ScrollWidth -gt $_.Width }) { throw 'Browser checks failed. See .local/browser-qa/report.json.' }
    if ($Full) { . (Join-Path $PSScriptRoot 'Test-PortalWorkflow.ps1') }
} finally {
    if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) { try { Invoke-Cdp 'Browser.close' | Out-Null } catch {} }
    $socket.Dispose()
    if (!$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}


