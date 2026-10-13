param([string]$ApiUrl = 'http://localhost:5100')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $root '.local/development.json') -Raw | ConvertFrom-Json
$credentials = @{ email = 'administrator@newsportal.local'; password = $config.Password } | ConvertTo-Json
$auth = Invoke-RestMethod "$ApiUrl/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $credentials
$headers = @{ Authorization = "Bearer $($auth.token)" }
$manifest = Get-Content (Join-Path $root 'assets/article-images/manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$articles = (Invoke-RestMethod "$ApiUrl/api/v1/articles/manage?pageSize=100" -Headers $headers).data
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $auth.token)
try {
    foreach ($entry in $manifest) {
        $article = $articles | Where-Object { $_.slug -eq $entry.slug } | Select-Object -First 1
        if (!$article) { throw "Missing demo article: $($entry.slug)" }
        $existingMedia = (Invoke-RestMethod "$ApiUrl/api/v1/media?search=$($entry.file)&pageSize=100" -Headers $headers).data |
            Where-Object { $_.originalFileName -eq $entry.file } | Select-Object -First 1
        if ($existingMedia -and $article.thumbnailUrl -eq $existingMedia.url) {
            Write-Host "Image already attached: $($article.title)"
            continue
        }
        $imagePath = Join-Path $root ('assets/article-images/' + $entry.file)
        $media = $existingMedia
        if (!$media) {
        $multipart = New-Object System.Net.Http.MultipartFormDataContent
        $image = New-Object System.Net.Http.ByteArrayContent(,[IO.File]::ReadAllBytes($imagePath))
        $image.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('image/png')
        $multipart.Add($image, 'file', $entry.file)
        try {
            $response = $client.PostAsync("$ApiUrl/api/v1/media", $multipart).GetAwaiter().GetResult()
            $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (!$response.IsSuccessStatusCode) { throw "Upload failed: $raw" }
            $media = $raw | ConvertFrom-Json
        } finally { $multipart.Dispose() }
        }
        $detail = Invoke-RestMethod "$ApiUrl/api/v1/articles/manage/$($article.id)" -Headers $headers
        $a = $detail.article
        $body = @{
            title = $a.title; slug = $a.slug; subtitle = $detail.subtitle
            summary = $a.summary; content = $detail.content; categoryId = $a.categoryId
            thumbnailUrl = $media.url; tagIds = @($detail.tags | ForEach-Object { $_.id })
            isFeatured = $a.isFeatured; isBreakingNews = $a.isBreakingNews; isEditorsChoice = $a.isEditorsChoice
            allowComments = $detail.allowComments; seoTitle = $detail.seoTitle
            seoDescription = $detail.seoDescription; rowVersion = $detail.rowVersion
        } | ConvertTo-Json -Depth 6
        Invoke-RestMethod "$ApiUrl/api/v1/articles/$($a.id)" -Method Put -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) | Out-Null
        Write-Host "Attached image: $($a.title)"
    }
} finally { $client.Dispose() }
