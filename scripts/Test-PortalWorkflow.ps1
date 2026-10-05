# Invoked by Test-ReaderBrowser.ps1 -Full after the Chrome session is ready.
if (!(Get-Command Invoke-Cdp -ErrorAction SilentlyContinue)) { throw 'Run Test-ReaderBrowser.ps1 -Full.' }
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'browser/workflow.js'))
Invoke-Cdp 'Page.addScriptToEvaluateOnNewDocument' @{ source = $source } | Out-Null
Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{ width = 1440; height = 1000; deviceScaleFactor = 1; mobile = $false } | Out-Null
$script:workflowChecks = @()
$config = Get-Content (Join-Path $root '.local/development.json') -Raw | ConvertFrom-Json
$suffix = [Guid]::NewGuid().ToString('N')
$email = "browser-$suffix@example.test"
$password = 'Qa!9aA' + $suffix
$slug = "browser-$suffix"
$articleId = $null; $mediaId = $null; $pollId = $null; $accountId = $null
$imagePath = Join-Path $output 'workflow-upload.png'
[IO.File]::WriteAllBytes($imagePath, [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII='))
function Js([string]$Expression) {
    $result = Invoke-Cdp 'Runtime.evaluate' @{ expression = $Expression; awaitPromise = $true; returnByValue = $true }
    if ($result.exceptionDetails) { throw $result.exceptionDetails.exception.description }
    return $result.result.value
}
function Wait-Js([string]$Expression) {
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        try { if (Js $Expression) { return } } catch { if ($attempt -eq 79) { throw } }
        Start-Sleep -Milliseconds 150
    }
    throw "Browser condition timed out: $Expression"
}
function Go([string]$Route, [string]$Selector) {
    Write-Host "Workflow: opening $Route"
    Invoke-Cdp 'Page.navigate' @{ url = $BaseUrl + $Route } | Out-Null
    $target = $Selector | ConvertTo-Json -Compress
    Wait-Js "document.readyState==='complete' && !!document.querySelector($target) && typeof qa==='object'"
}
function Check([string]$Name) {
    if (@($script:exceptions).Count -gt 0) { throw "Unexpected JavaScript exception during $Name" }
    $script:workflowChecks += $Name
    Write-Output "PASS: $Name"
}
function Sign-In([string]$Email, [string]$Password) {
    Go '/dang-nhap' '#auth-form'
    $data = @{email=$Email;password=$Password} | ConvertTo-Json -Compress
    Js "qa.fill('#auth-form',$data).requestSubmit(); true" | Out-Null
    Wait-Js "location.pathname==='/' && !!document.querySelector('#logout')"
}
function Staff([string]$Role) { Sign-In ($Role+'@newsportal.local') $config.Password }
function Search-Article {
    Go '/cms?section=articles' '#cms-filter'
    # Search the title, because the public search intentionally does not search slugs.
    $filter = @{search=('QA '+$suffix)} | ConvertTo-Json -Compress
    Js "qa.fill('#cms-filter',$filter).requestSubmit(); true" | Out-Null
    Wait-Js "document.querySelectorAll('#cms-content tbody tr').length===1 && !!document.querySelector('.edit[data-id=`"$articleId`"]')"
    Js "qa.openArticle($articleId)" | Out-Null
}
$script:exceptions = @()
try {
    Go '/dang-ky' '#auth-form'
    $registration = @{email=$email;password=$password;fullName='QA Browser Reader'} | ConvertTo-Json -Compress
    Js "qa.fill('#auth-form',$registration).requestSubmit(); true" | Out-Null
    Wait-Js "location.pathname==='/' && !!document.querySelector('#logout')"
    $accountId = Js 'me.id'
    Check 'Reader registration through the web form'
    Go '/tai-khoan' '#profile-form'
    Js "qa.fill('#profile-form',{fullName:'QA Browser Updated',bio:'Updated through the UI'}).requestSubmit(); true" | Out-Null
    Wait-Js "me.fullName==='QA Browser Updated'"
    Check 'Profile update'
    Js "qa.click('#logout'); true" | Out-Null
    Wait-Js "location.pathname==='/' && document.querySelector('#account-nav a')?.getAttribute('href')==='/dang-nhap'"
    Check 'Logout removes the web session'

    Staff 'journalist'
    Go '/cms?section=media' '#upload-form'
    $doc = Invoke-Cdp 'DOM.getDocument'
    $input = Invoke-Cdp 'DOM.querySelector' @{ nodeId = $doc.root.nodeId; selector = '#upload-form input[type=file]' }
    Invoke-Cdp 'DOM.setFileInputFiles' @{ nodeId = $input.nodeId; files = @($imagePath) } | Out-Null
    Js "document.querySelector('#upload-form').requestSubmit(); true" | Out-Null
    Wait-Js "!!document.querySelector('.media-grid img')"
    $media = Js "(async()=>{const r=await api('media?search=workflow-upload.png');return r.data[0];})()"
    $mediaId = $media.id
    Check 'Image upload using a real file input and HTTP gateway'
    Go '/cms?section=articles' '#new-article'
    $articleData = @{ title=('QA '+$suffix); slug=$slug; image=$media.url } | ConvertTo-Json -Compress
    $articleId = Js "qa.createArticle($articleData)"
    Check 'Journalist creates, previews, edits, views revisions and submits article'

    Staff 'editor'; Search-Article
    Js "qa.review('reject')" | Out-Null
    Check 'Editor rejects article with review note'
    Staff 'journalist'; Search-Article
    Js "qa.assert(document.querySelector('.notice').textContent.includes('QA: revise'),'Missing review note');qa.review('submit')" | Out-Null
    Staff 'editor'; Search-Article
    Js "qa.review('approve')" | Out-Null
    Js "qa.review('publish')" | Out-Null
    Check 'Rejected article resubmitted, approved and published'
    Go ('/tin-tuc/'+$slug) '#save'
    Js "qa.assert(document.title==='QA SEO title','SEO title mismatch');qa.assert(document.querySelector('meta[name=description]').content==='QA SEO description','SEO description mismatch')" | Out-Null
    Check 'Published article and SEO metadata'
    Js "qa.assert(!document.querySelector('.article-body img')&&!window.qaXss,'Stored content executed as HTML');qa.click('#share');qa.wait(()=>!document.querySelector('#toast').hidden)" | Out-Null
    Check 'Stored HTML is escaped and share button responds'

    Sign-In $email $password
    Go ('/tin-tuc/'+$slug) '#comment-form'
    $commentId = Js "qa.reader({id:$articleId})"
    Go '/da-luu' '.card-news'
    Js "qa.assert(document.querySelector('#app').textContent.includes('QA $suffix'),'Bookmark absent')" | Out-Null
    Go '/lich-su' '.card-news'
    Js "qa.assert(document.querySelector('#app').textContent.includes('QA $suffix'),'Reading history absent')" | Out-Null
    Check 'Reader bookmark, reaction, pending comment and history'
    Staff 'moderator'
    Go '/cms?section=comments' '.moderate'
    Js "qa.click('.moderate[data-id=`"$commentId`"][data-status=Approved]');true" | Out-Null
    Wait-Js "[...document.querySelectorAll('.comment')].some(c=>c.textContent.includes('QA browser comment')&&c.textContent.includes('Approved'))"
    Check 'Moderator approves comment'
    Sign-In $email $password
    Go ('/tin-tuc/'+$slug) '#comment-form'
    Wait-Js "!!document.querySelector('.reply[data-id=`"$commentId`"]')"
    Js "qa.click('.reply[data-id=`"$commentId`"]');qa.fill('#comment-form',{content:'QA browser reply'}).requestSubmit();true" | Out-Null
    Wait-Js "document.querySelector('#comments').textContent.includes('QA browser reply')"
    Js "const oldPrompt=window.prompt;window.prompt=()=> 'QA comment edited';qa.click('.edit-comment[data-id=`"$commentId`"]');window.prompt=oldPrompt;true" | Out-Null
    Wait-Js "document.querySelector('#comments').textContent.includes('QA comment edited')"
    Js "const oldConfirm=window.confirm;window.confirm=()=>true;qa.click('.delete-comment[data-id=`"$commentId`"]');window.confirm=oldConfirm;true" | Out-Null
    Wait-Js "!document.querySelector('.delete-comment[data-id=`"$commentId`"]')"
    Check 'Reader replies, edits and deletes own comment'

    Staff 'administrator'
    Go '/cms' '.stats'
    Js "qa.assert(document.querySelectorAll('.stat').length===7,'Dashboard cards missing')" | Out-Null
    Check 'Administrator dashboard loads database metrics'
    foreach ($kind in @('categories','tags')) {
        Go ("/cms?section=$kind") '#tax-form'
        Js "qa.taxonomy('$kind','qa-$suffix')" | Out-Null
        Check "CMS $kind create, edit and delete"
    }
    Go '/cms?section=users' '#user-search'
    $search=@{search=$email}|ConvertTo-Json -Compress
    Js "qa.fill('#user-search',$search).requestSubmit();true" | Out-Null
    Wait-Js "document.querySelectorAll('tbody tr').length===1"
    Js "qa.click('.user-detail');qa.wait(()=>!document.querySelector('#user-detail').hidden)" | Out-Null
    Js "qa.access('$accountId','Moderator',true)" | Out-Null
    Js "qa.access('$accountId','User',false)" | Out-Null
    Js "qa.access('$accountId','User',true)" | Out-Null
    Check 'CMS user search, detail, role change, disable and restore'
    Go '/cms?section=settings' '#settings-form'
    # Save the existing values so the QA run does not change user content.
    Js "document.querySelector('#settings-form').requestSubmit();qa.wait(()=>!document.querySelector('#settings-form button').disabled)" | Out-Null
    Check 'CMS settings save'
    Go '/cms?section=polls' '#poll-form'
    $pollId = Js "qa.poll('QA poll $suffix')"
    Check 'CMS creates a poll'
    Sign-In $email $password
    Go '/cong-dong' '#subscription'
    Js "qa.click('#subscription');qa.wait(async()=> (await api('newsletter/me')).subscribed)" | Out-Null
    Js "document.querySelector('.vote-form[data-id=`"$pollId`"] input').checked=true;document.querySelector('.vote-form[data-id=`"$pollId`"]').requestSubmit();qa.wait(async()=> (await api('polls')).data.find(p=>p.id===$pollId).myOptionId!==null)" | Out-Null
    Check 'Reader newsletter subscription and poll voting'
    Staff 'administrator'
    Go '/cms?section=newsletter' '#newsletter-search'
    Js "qa.fill('#newsletter-search',$search).requestSubmit();qa.wait(()=>document.querySelector('tbody').textContent.includes('$email'))" | Out-Null
    Go '/cms?section=polls' '.poll-state'
    Js "qa.click('.poll-state[data-id=`"$pollId`"]');qa.wait(async()=>!(await api('polls?all=true')).data.find(p=>p.id===$pollId).isActive)" | Out-Null
    Check 'CMS newsletter list and poll close'
    Sign-In $email $password
    Go '/cong-dong' '#subscription'
    Js "qa.click('#subscription');qa.wait(async()=>!(await api('newsletter/me')).subscribed)" | Out-Null
    Check 'Reader newsletter unsubscribe'
    Go '/da-luu' '.card-news'
    Js "qa.click('.card-news button');qa.wait(async()=>!(await api('bookmarks')).data.some(a=>a.id===$articleId))" | Out-Null
    Go ('/tin-tuc/'+$slug) '#unreact'
    Js "qa.click('.react[data-type=Love]');qa.wait(()=>document.querySelector('#reaction-count').textContent.includes('Love'))" | Out-Null
    Js "qa.click('#unreact');qa.wait(async()=>(await api('articles/$articleId/reactions')).length===0)" | Out-Null
    Check 'Reader removes bookmark and changes/removes reaction'

    Go '/tim-kiem' '#search-form'
    Js 'qa.pagerFailure()' | Out-Null
    Js "document.querySelector('body').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{ type='keyDown'; key='Tab'; code='Tab'; windowsVirtualKeyCode=9 } | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{ type='keyUp'; key='Tab'; code='Tab'; windowsVirtualKeyCode=9 } | Out-Null
    Js "qa.assert(document.activeElement!==document.body,'Keyboard focus did not move')" | Out-Null
    Check 'Pagination error recovery and keyboard Tab navigation'
    Staff 'administrator'
    foreach ($width in @(1440,390)) {
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=$width;height=900;deviceScaleFactor=1;mobile=($width -eq 390)} | Out-Null
        foreach ($section in @('dashboard','articles','categories','tags','comments','media','users','settings','newsletter','polls')) {
            Go ("/cms?section=$section") '#cms-content h1'
            Start-Sleep -Milliseconds 200
            Js "qa.assert(document.documentElement.scrollWidth<=innerWidth,'Horizontal overflow in CMS $section at $width');qa.assert([...document.images].every(i=>!i.complete||i.naturalWidth>0),'Broken CMS image')" | Out-Null
            $shot=Invoke-Cdp 'Page.captureScreenshot' @{format='png'}
            [IO.File]::WriteAllBytes((Join-Path $output ("cms-$section-$width.png")),[Convert]::FromBase64String($shot.data))
        }
        Go ('/tin-tuc/'+$slug) '#save'
        Js "qa.assert(document.documentElement.scrollWidth<=innerWidth,'Article overflows viewport')" | Out-Null
        Go '/chuyen-muc/cong-nghe' '.category-lead'
        Go '/tag/chu-de-1' '#results'
        Js "qa.assert(document.documentElement.scrollWidth<=innerWidth,'Reader listing overflows viewport')" | Out-Null
        Check "CMS all sections and article/category/tag responsive at $width px"
    }
    Go '/cms?section=polls' '.poll-state'
    Js "qa.click('.poll-state[data-id=`"$pollId`"]');qa.wait(async()=>(await api('polls?all=true')).data.find(p=>p.id===$pollId).isActive)" | Out-Null
    Js "window.confirm=()=>true;qa.click('.poll-delete[data-id=`"$pollId`"]');qa.wait(()=>!document.querySelector('.poll-delete[data-id=`"$pollId`"]'))" | Out-Null
    $pollId=$null
    Search-Article
    Js "qa.review('archive')" | Out-Null
    Js "qa.click('#back');qa.wait(()=>!!document.querySelector('.delete[data-id=`"$articleId`"]'))" | Out-Null
    Js "window.confirm=()=>true;qa.click('.delete[data-id=`"$articleId`"]');qa.wait(()=>!document.querySelector('.delete[data-id=`"$articleId`"]'))" | Out-Null
    Go '/cms?section=media' '.media-grid'
    Js "window.confirm=()=>true;qa.click('.delete[data-id=`"$mediaId`"]');qa.wait(()=>!document.querySelector('.delete[data-id=`"$mediaId`"]'))" | Out-Null
    $mediaId=$null
    Check 'CMS reopens/deletes poll, archives/deletes article and deletes unused image'
    $shot=Invoke-Cdp 'Page.captureScreenshot' @{format='png'}
    [IO.File]::WriteAllBytes((Join-Path $output 'workflow-final.png'),[Convert]::FromBase64String($shot.data))
} catch {
    $shot=Invoke-Cdp 'Page.captureScreenshot' @{format='png'}
    [IO.File]::WriteAllBytes((Join-Path $output 'workflow-failure.png'),[Convert]::FromBase64String($shot.data))
    throw
} finally {
    $script:workflowChecks | ConvertTo-Json | Set-Content (Join-Path $output 'workflow-report.json') -Encoding UTF8
    # Cleanup uses only IDs/slugs created by this run. Never remove demo/user content.
    $auth=Invoke-RestMethod 'http://localhost:5100/api/v1/auth/login' -Method Post -ContentType 'application/json' -Body (@{email='administrator@newsportal.local';password=$config.Password}|ConvertTo-Json)
    $headers=@{Authorization=('Bearer '+$auth.token)}
    $articles=Invoke-RestMethod ('http://localhost:5100/api/v1/articles/manage?search=QA%20'+$suffix) -Headers $headers
    foreach($a in $articles.data | Where-Object {$_.slug -eq $slug}) { Invoke-RestMethod ("http://localhost:5100/api/v1/articles/"+$a.id) -Method Delete -Headers $headers | Out-Null }
    if($pollId) { Invoke-RestMethod ("http://localhost:5100/api/v1/polls/$pollId") -Method Delete -Headers $headers | Out-Null }
    if($mediaId) { Invoke-RestMethod ("http://localhost:5100/api/v1/media/$mediaId") -Method Delete -Headers $headers | Out-Null }
    foreach($kind in @('categories','tags')) {
        $items=Invoke-RestMethod ("http://localhost:5100/api/v1/$kind`?search=QA&pageSize=100&all=true") -Headers $headers
        foreach($item in $items.data | Where-Object {$_.slug -eq "qa-$suffix"}) { Invoke-RestMethod ("http://localhost:5100/api/v1/$kind/"+$item.id) -Method Delete -Headers $headers | Out-Null }
    }
    $dbConfig=Get-Content (Join-Path $root 'NewsPortal.Api/appsettings.Development.json') -Raw|ConvertFrom-Json
    $connection=New-Object System.Data.SqlClient.SqlConnection($dbConfig.ConnectionStrings.DefaultConnection)
    try {
        $connection.Open(); $command=$connection.CreateCommand()
        $command.CommandText='SET XACT_ABORT ON; BEGIN TRANSACTION; DECLARE @id nvarchar(450)=(SELECT Id FROM AspNetUsers WHERE Email=@email); IF @id IS NOT NULL BEGIN DELETE FROM AuditLogs WHERE UserId=@id; DELETE FROM Notifications WHERE UserId=@id; DELETE FROM PollVotes WHERE UserId=@id; DELETE FROM Bookmarks WHERE UserId=@id; DELETE FROM ReadingHistories WHERE UserId=@id; DELETE FROM ArticleReactions WHERE UserId=@id; DELETE FROM AspNetUsers WHERE Id=@id AND Email=@email; END; COMMIT;'
        $null=$command.Parameters.AddWithValue('@email',$email);$null=$command.ExecuteNonQuery()
    } finally { $connection.Dispose() }
}

