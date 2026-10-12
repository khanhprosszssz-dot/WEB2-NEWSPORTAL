using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using Xunit;
namespace NewsPortal.Tests;

public class ExtendedTests
{
    static readonly Uri ApiAddress = new("http://localhost:5100/api/v1/");
    static HttpClient Client() => new() { BaseAddress = ApiAddress };
    static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "NewsPortal.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Run tests from the NewsPortal workspace.");
    }
    static async Task<HttpClient> Staff(string role)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root(), ".local/development.json")));
        return await SignIn(role + "@newsportal.local", config.RootElement.GetProperty("Password").GetString()!);
    }
    static async Task<HttpClient> SignIn(string email, string password)
    {
        var c = Client();
        var r = await Json(await c.PostAsJsonAsync("auth/login", new { email, password }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", r.GetProperty("token").GetString());
        return c;
    }
    sealed class Account : IAsyncDisposable
    {
        public string Email { get; } = "integration-" + Guid.NewGuid().ToString("N") + "@example.test";
        public string Password { get; } = "Np!9aA" + Guid.NewGuid().ToString("N");
        public string Id { get; private set; } = "";
        public HttpClient Http { get; } = Client();
        public static async Task<Account> Create()
        {
            var a = new Account();
            var response = await a.Http.PostAsJsonAsync("auth/register", new { email = a.Email, password = a.Password, fullName = "Bạn đọc kiểm thử", roles = new[] { "Administrator" } });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var r = await Json(response); a.Id = r.GetProperty("user").GetProperty("id").GetString()!;
            Assert.Equal("User", r.GetProperty("user").GetProperty("roles")[0].GetString());
            a.Http.DefaultRequestHeaders.Authorization = new("Bearer", r.GetProperty("token").GetString());
            return a;
        }
        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root(), "NewsPortal.Api/appsettings.Development.json")));
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            // Only delete the account created by this test, guarded by both generated ID and email.
            if (!await db.Users.AnyAsync(u => u.Id == Id && u.Email == Email)) return;
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.AuditLogs.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.Notifications.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.PollVotes.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.ReadingHistories.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.Bookmarks.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.ArticleReactions.Where(x => x.UserId == Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == Id && u.Email == Email).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
    }
    [Fact]
    public async Task RegistrationProfileRoleChangeAndDisableRevokeTokens()
    {
        await using var a = await Account.Create(); using var admin = await Staff("administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Http.PostAsJsonAsync("auth/register", new { email = a.Email, password = a.Password, fullName = "Duplicate" })).StatusCode);
        var changed = await Json(await a.Http.PutAsJsonAsync("auth/me", new { fullName = "Tên đã đổi", bio = "Giới thiệu tiếng Việt" }));
        Assert.Equal("Tên đã đổi", changed.GetProperty("fullName").GetString());
        var list = await Json(await admin.GetAsync("admin/users?search=" + Uri.EscapeDataString(a.Email)));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var detail = await Json(await admin.GetAsync("admin/users/" + a.Id));
        Assert.False(detail.TryGetProperty("passwordHash", out _));
        (await admin.PutAsJsonAsync("admin/users/" + a.Id, new { role = "Moderator", isActive = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Http.GetAsync("auth/me")).StatusCode);
        using var moderator = await SignIn(a.Email, a.Password);
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("comments/manage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.GetAsync("articles/manage")).StatusCode);
        (await admin.PutAsJsonAsync("admin/users/" + a.Id, new { role = "User", isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await moderator.GetAsync("auth/me")).StatusCode);
        using var anon = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("auth/login", new { email = a.Email, password = a.Password })).StatusCode);
        (await admin.PutAsJsonAsync("admin/users/" + a.Id, new { role = "User", isActive = true })).EnsureSuccessStatusCode();
        using var restored = await SignIn(a.Email, a.Password);
        Assert.Equal(HttpStatusCode.OK, (await restored.GetAsync("auth/me")).StatusCode);
    }
    [Fact]
    public async Task WebCookieSessionUsesCsrfAndDoesNotExposeJwt()
    {
        await using var a = await Account.Create();
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var web = new HttpClient(handler) { BaseAddress = new("http://localhost:5200/") };
        async Task Csrf()
        {
            var html = await web.GetStringAsync("dang-nhap");
            var match = Regex.Match(html, "name=\"csrf-token\" content=\"([^\"]+)\""); Assert.True(match.Success);
            web.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            web.DefaultRequestHeaders.Add("X-CSRF-TOKEN", WebUtility.HtmlDecode(match.Groups[1].Value));
        }
        await Csrf();
        var user = await Json(await web.PostAsJsonAsync("gateway/auth/login", new { email = a.Email, password = a.Password }));
        Assert.False(user.TryGetProperty("token", out _));
        Assert.True(handler.CookieContainer.GetCookies(web.BaseAddress)["NewsPortal.Session"]?.HttpOnly);
        await Csrf();
        var profile = await Json(await web.PutAsJsonAsync("gateway/auth/me", new { fullName = "Hồ sơ từ website", bio = "HTTP gateway" }));
        Assert.Equal("Hồ sơ từ website", profile.GetProperty("fullName").GetString());
        (await web.PostAsync("gateway/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.GetAsync("gateway/auth/me")).StatusCode);
    }
    [Fact]
    public async Task NewsletterAndPollsPersistRespectRolesAndValidate()
    {
        await using var a = await Account.Create(); using var admin = await Staff("administrator"); using var anon = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("newsletter", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await a.Http.PostAsync("newsletter", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Http.PostAsync("newsletter", null)).StatusCode);
        Assert.True((await Json(await a.Http.GetAsync("newsletter/me"))).GetProperty("subscribed").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.GetAsync("newsletter")).StatusCode);
        var subscribers = await Json(await admin.GetAsync("newsletter?search=" + Uri.EscapeDataString(a.Email)));
        Assert.Equal(1, subscribers.GetProperty("totalCount").GetInt32());
        (await a.Http.DeleteAsync("newsletter/me")).EnsureSuccessStatusCode();
        Assert.False((await Json(await a.Http.GetAsync("newsletter/me"))).GetProperty("subscribed").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("polls", new { question = "Trùng lựa chọn", options = new[] { "A", " A " } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("polls", new { question = "Null", options = (string[]?)null })).StatusCode);
        var poll = await Json(await admin.PostAsJsonAsync("polls", new { question = "Khảo sát kiểm thử", options = new[] { "Lựa chọn A", "Lựa chọn B" } }));
        var id = poll.GetProperty("id").GetInt32();
        try {
            var all = await Json(await a.Http.GetAsync("polls?pageSize=100"));
            var p = all.GetProperty("data").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id);
            var options = p.GetProperty("options"); var first = options[0].GetProperty("id").GetInt32(); var second = options[1].GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync($"polls/{id}/vote", new { optionId = first })).StatusCode);
            (await a.Http.PutAsJsonAsync($"polls/{id}/vote", new { optionId = first })).EnsureSuccessStatusCode();
            (await a.Http.PutAsJsonAsync($"polls/{id}/vote", new { optionId = second })).EnsureSuccessStatusCode();
            all = await Json(await a.Http.GetAsync("polls?pageSize=100"));
            p = all.GetProperty("data").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id);
            Assert.Equal(second, p.GetProperty("myOptionId").GetInt32());
            Assert.Equal(1, p.GetProperty("options").EnumerateArray().Sum(o => o.GetProperty("votes").GetInt32()));
            (await admin.PatchAsJsonAsync("polls/" + id, new { isActive = false })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Http.PutAsJsonAsync($"polls/{id}/vote", new { optionId = first })).StatusCode);
        } finally { (await admin.DeleteAsync("polls/" + id)).EnsureSuccessStatusCode(); }
    }
    [Fact]
    public async Task ReadingHistoryRepliesNotificationsAndCommentOwnership()
    {
        await using var a = await Account.Create(); await using var b = await Account.Create();
        using var editor = await Staff("editor"); using var moderator = await Staff("moderator");
        var cats = await Json(await editor.GetAsync("categories"));
        var article = await Json(await editor.PostAsJsonAsync("articles", new { title = "Bài kiểm thử tương tác", slug = "reply-" + Guid.NewGuid().ToString("N"), summary = "Kiểm thử", content = "Nội dung bài viết dành cho kiểm thử tương tác bạn đọc.", categoryId = cats.GetProperty("data")[0].GetProperty("id").GetInt32(), allowComments = true }));
        var id = article.GetProperty("id").GetInt32();
        try {
            foreach (var action in new[] { "submit", "approve", "publish" }) (await editor.PostAsJsonAsync($"articles/{id}/review", new { action })).EnsureSuccessStatusCode();
            (await a.Http.PostAsync($"articles/{id}/view", null)).EnsureSuccessStatusCode();
            var history = await Json(await a.Http.GetAsync("history")); Assert.Contains(history.GetProperty("data").EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
            var parent = await Json(await a.Http.PostAsJsonAsync($"articles/{id}/comments", new { content = "Ý kiến đầu tiên" })); var cid = parent.GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.Forbidden, (await b.Http.PutAsJsonAsync("comments/" + cid, new { content = "Không được sửa của người khác" })).StatusCode);
            (await moderator.PatchAsJsonAsync($"comments/{cid}/status", new { status = "Approved" })).EnsureSuccessStatusCode();
            var reply = await Json(await b.Http.PostAsJsonAsync($"articles/{id}/comments", new { content = "Phản hồi ý kiến", parentCommentId = cid }));
            (await moderator.PatchAsJsonAsync($"comments/{reply.GetProperty("id").GetInt32()}/status", new { status = "Approved" })).EnsureSuccessStatusCode();
            var messages = await Json(await a.Http.GetAsync("notifications")); var n = messages.GetProperty("data")[0];
            Assert.Equal("Có phản hồi mới", n.GetProperty("title").GetString()); var nid = n.GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.NotFound, (await b.Http.PatchAsync($"notifications/{nid}/read", null)).StatusCode);
            (await a.Http.PatchAsync($"notifications/{nid}/read", null)).EnsureSuccessStatusCode();
            messages = await Json(await a.Http.GetAsync("notifications")); Assert.True(messages.GetProperty("data")[0].GetProperty("isRead").GetBoolean());
            (await a.Http.PutAsJsonAsync("comments/" + cid, new { content = "Ý kiến đã cập nhật" })).EnsureSuccessStatusCode();
            (await a.Http.DeleteAsync("comments/" + cid)).EnsureSuccessStatusCode();
        } finally { (await editor.DeleteAsync("articles/" + id)).EnsureSuccessStatusCode(); }
    }
    [Fact]
    public async Task WebGatewayForwardsMultipartAfterMvcFormBinding()
    {
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var web = new HttpClient(handler) { BaseAddress = new("http://localhost:5200/") };
        async Task Csrf()
        {
            var html = await web.GetStringAsync("dang-nhap");
            var match = Regex.Match(html, "name=\"csrf-token\" content=\"([^\"]+)\"");
            Assert.True(match.Success);
            web.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            web.DefaultRequestHeaders.Add("X-CSRF-TOKEN", WebUtility.HtmlDecode(match.Groups[1].Value));
        }
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root(), ".local/development.json")));
        await Csrf();
        (await web.PostAsJsonAsync("gateway/auth/login", new { email = "journalist@newsportal.local", password = config.RootElement.GetProperty("Password").GetString() })).EnsureSuccessStatusCode();
        await Csrf();
        using var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));
        image.Headers.ContentType = new("image/png"); content.Add(image, "file", "gateway-regression.png");
        var response = await web.PostAsync("gateway/media", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var media = await Json(response); var id = media.GetProperty("id").GetInt32();
        try {
            var name = media.GetProperty("url").GetString()!.Split('/').Last();
            var served = await web.GetAsync("media/" + name);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await served.Content.ReadAsByteArrayAsync());
        } finally { (await web.DeleteAsync("gateway/media/" + id)).EnsureSuccessStatusCode(); }
    }
    [Fact]
    public async Task OpenApiDocumentsSuccessSchemasAndEndpointSecurity()
    {
        using var http = new HttpClient();
        var spec = await Json(await http.GetAsync("http://localhost:5100/swagger/v1/swagger.json"));
        var paths = spec.GetProperty("paths");
        var create = paths.GetProperty("/api/v1/articles").GetProperty("post");
        Assert.True(create.GetProperty("responses").TryGetProperty("201", out var created));
        Assert.True(created.GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));
        Assert.True(create.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(create.GetProperty("responses").TryGetProperty("403", out _));
        Assert.Equal(1, create.GetProperty("security").GetArrayLength());
        var get = paths.GetProperty("/api/v1/articles").GetProperty("get");
        Assert.True(!get.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);
        Assert.True(paths.GetProperty("/api/v1/articles/{id}").GetProperty("put").GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(paths.GetProperty("/api/v1/media").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));
        var download = paths.GetProperty("/api/v1/media/{id}/download").GetProperty("get");
        Assert.Equal("binary", download.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/octet-stream").GetProperty("schema").GetProperty("format").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("http://localhost:5100/api/v1/media/1/download")).StatusCode);
    }
    [Fact]
    public void PublicCacheEtagsCannotRepeatAfterAnApplicationRestart()
    {
        using var firstMemory = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        using var secondMemory = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var first = new NewsPortal.Api.Services.PublicCache(firstMemory);
        var restarted = new NewsPortal.Api.Services.PublicCache(secondMemory);
        var oldKey = first.Key("/api/v1/categories");
        Assert.Equal(oldKey, first.Key("/api/v1/categories"));
        Assert.NotEqual(oldKey, restarted.Key("/api/v1/categories"));
        first.Invalidate();
        Assert.NotEqual(oldKey, first.Key("/api/v1/categories"));
    }
    [Fact]
    public async Task ArticlesRejectMissingMediaAndContentTooShortAfterTrimming()
    {
        using var editor = await Staff("editor");
        var categories = await Json(await editor.GetAsync("categories"));
        foreach (var scenario in new[] { "media", "title", "content" }) {
            var response = await editor.PostAsJsonAsync("articles", new {
                title = scenario == "title" ? "a    " : "Audit validation article",
                slug = "audit-" + Guid.NewGuid().ToString("N"), summary = "Audit validation",
                content = scenario == "content" ? "x" + new string(' ', 30) : "Valid article content with more than twenty characters.",
                categoryId = categories.GetProperty("data")[0].GetProperty("id").GetInt32(),
                thumbnailUrl = scenario == "media" ? "/uploads/" + Guid.NewGuid().ToString("N") + ".png" : null
            });
            // Clean up even on the unfixed implementation, before reporting failure.
            if (response.StatusCode == HttpStatusCode.Created) {
                var article = await Json(response);
                (await editor.DeleteAsync("articles/" + article.GetProperty("id").GetInt32())).EnsureSuccessStatusCode();
            }
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
    [Fact]
    public async Task QueryOrderingPagingAndCombinedFiltersMatchReturnedArticles()
    {
        using var anon = Client();
        var asc = await Json(await anon.GetAsync("articles?pageSize=100&sortColumn=publishedAt&sortOrder=asc"));
        var desc = await Json(await anon.GetAsync("articles?pageSize=100&sortColumn=publishedAt&sortOrder=desc"));
        var rows = asc.GetProperty("data").EnumerateArray().ToArray();
        Assert.NotEmpty(rows);
        var dates = rows.Select(a => a.GetProperty("publishedAt").GetDateTime()).ToArray();
        Assert.Equal(dates.OrderBy(d => d), dates);
        var descending = desc.GetProperty("data").EnumerateArray().Select(a => a.GetProperty("publishedAt").GetDateTime()).ToArray();
        Assert.Equal(descending.OrderByDescending(d => d), descending);
        var a = rows[0];
        var detail = await Json(await anon.GetAsync("articles/" + a.GetProperty("id").GetInt32()));
        var tag = detail.GetProperty("tags")[0].GetProperty("slug").GetString();
        var date = a.GetProperty("publishedAt").GetDateTime();
        var query = "articles?category=" + a.GetProperty("categorySlug").GetString() + "&tag=" + tag + "&author=" + a.GetProperty("authorId").GetString()
            + "&fromDate=" + Uri.EscapeDataString(date.AddSeconds(-1).ToString("O")) + "&toDate=" + Uri.EscapeDataString(date.AddSeconds(1).ToString("O"));
        var filtered = await Json(await anon.GetAsync(query));
        Assert.Contains(filtered.GetProperty("data").EnumerateArray(), item => item.GetProperty("id").GetInt32() == a.GetProperty("id").GetInt32());
        Assert.All(filtered.GetProperty("data").EnumerateArray(), item => {
            Assert.Equal(a.GetProperty("categoryId").GetInt32(), item.GetProperty("categoryId").GetInt32());
            Assert.Equal(a.GetProperty("authorId").GetString(), item.GetProperty("authorId").GetString());
            Assert.InRange(item.GetProperty("publishedAt").GetDateTime(), date.AddSeconds(-1), date.AddSeconds(1));
        });
        var page1 = await Json(await anon.GetAsync("articles?pageSize=3&pageIndex=1"));
        var page2 = await Json(await anon.GetAsync("articles?pageSize=3&pageIndex=2"));
        Assert.Empty(page1.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetInt32())
            .Intersect(page2.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetInt32())));
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("articles?fromDate=2026-02-01&toDate=2026-01-01")).StatusCode);
    }
    [Fact]
    public async Task OwnershipAndInactiveCategoriesPreventPrivateContentDisclosure()
    {
        await using var account = await Account.Create();
        using var admin = await Staff("administrator"); using var editor = await Staff("editor"); using var anon = Client();
        (await admin.PutAsJsonAsync("admin/users/" + account.Id, new { role = "Journalist", isActive = true })).EnsureSuccessStatusCode();
        using var journalist = await SignIn(account.Email, account.Password);
        var slug = "visibility-" + Guid.NewGuid().ToString("N");
        var category = await Json(await editor.PostAsJsonAsync("categories", new { name = "Audit visibility", slug }));
        var categoryId = category.GetProperty("id").GetInt32();
        int? articleId = null;
        try {
            var input = new { title = "Audit private article", slug, summary = "Private visibility test", content = "A private article to validate ownership and public visibility rules.", categoryId };
            var article = await Json(await editor.PostAsJsonAsync("articles", input)); articleId = article.GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("articles/" + articleId)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("articles/manage/" + articleId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await journalist.GetAsync("articles/manage/" + articleId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await journalist.GetAsync($"articles/{articleId}/revisions")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await journalist.PutAsJsonAsync("articles/" + articleId, input)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await journalist.DeleteAsync("articles/" + articleId)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await journalist.PostAsJsonAsync($"articles/{articleId}/review", new { action = "submit" })).StatusCode);
            foreach (var action in new[] { "submit", "approve", "publish" }) (await editor.PostAsJsonAsync($"articles/{articleId}/review", new { action })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("articles/" + articleId)).StatusCode);
            using var web = new HttpClient { BaseAddress = new("http://localhost:5200/") };
            Assert.Contains(slug, await web.GetStringAsync("sitemap.xml"));
            Assert.Contains(slug, await web.GetStringAsync("rss"));
            (await editor.PutAsJsonAsync("categories/" + categoryId, new { name = "Audit visibility", slug, isActive = false })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("articles/" + articleId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"articles/{articleId}/comments")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"articles/{articleId}/reactions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await journalist.PostAsync("bookmarks/" + articleId, null)).StatusCode);
            var list = await Json(await anon.GetAsync("articles?category=" + slug));
            Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
            Assert.DoesNotContain(slug, await web.GetStringAsync("sitemap.xml"));
            Assert.DoesNotContain(slug, await web.GetStringAsync("rss"));
        } finally {
            if (articleId.HasValue) (await editor.DeleteAsync("articles/" + articleId)).EnsureSuccessStatusCode();
            (await editor.DeleteAsync("categories/" + categoryId)).EnsureSuccessStatusCode();
        }
    }
    [Fact]
    public async Task ConcurrentArticleEditsAllowExactlyOneWinner()
    {
        using var editor = await Staff("editor");
        var categories = await Json(await editor.GetAsync("categories"));
        var input = new Dictionary<string, object?> {
            ["title"] = "Concurrent editing audit", ["slug"] = "concurrency-" + Guid.NewGuid().ToString("N"),
            ["summary"] = "Concurrent writers must not overwrite each other.",
            ["content"] = "An article used to verify simultaneous optimistic concurrency protection.",
            ["categoryId"] = categories.GetProperty("data")[0].GetProperty("id").GetInt32()
        };
        var created = await Json(await editor.PostAsJsonAsync("articles", input));
        var id = created.GetProperty("id").GetInt32();
        try {
            var detail = await Json(await editor.GetAsync("articles/manage/" + id));
            input["rowVersion"] = detail.GetProperty("rowVersion").GetString();
            var first = new Dictionary<string, object?>(input) { ["title"] = "Concurrent writer first" };
            var second = new Dictionary<string, object?>(input) { ["title"] = "Concurrent writer second" };
            var responses = await Task.WhenAll(editor.PutAsJsonAsync("articles/" + id, first), editor.PutAsJsonAsync("articles/" + id, second));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            var winner = responses[0].StatusCode == HttpStatusCode.NoContent ? first : second;
            detail = await Json(await editor.GetAsync("articles/manage/" + id));
            Assert.Equal(winner["title"], detail.GetProperty("article").GetProperty("title").GetString());
            var revisions = await Json(await editor.GetAsync($"articles/{id}/revisions"));
            Assert.Equal(1, revisions.GetProperty("totalCount").GetInt32());
            foreach (var response in responses) response.Dispose();
        } finally { (await editor.DeleteAsync("articles/" + id)).EnsureSuccessStatusCode(); }
    }
    [Fact]
    public async Task ExpiredWrongIssuerAudienceAndSignatureTokensAreRejected()
    {
        using var signedIn = await Staff("user");
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var original = handler.ReadJwtToken(signedIn.DefaultRequestHeaders.Authorization!.Parameter!);
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root(), ".local/development.json")));
        var secret = config.RootElement.GetProperty("JwtKey").GetString()!;
        using var client = Client();
        foreach (var scenario in new[] { "expired", "issuer", "audience", "signature" }) {
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(scenario == "signature" ? new string('x', 64) : secret));
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: scenario == "issuer" ? "WrongIssuer" : original.Issuer,
                audience: scenario == "audience" ? "WrongAudience" : original.Audiences.Single(),
                claims: original.Claims.Where(c => c.Type is not ("iss" or "aud" or "exp" or "nbf" or "iat")),
                notBefore: DateTime.UtcNow.AddHours(-1),
                expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", handler.WriteToken(token));
            var response = await client.GetAsync("auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task SitemapOnlyContainsPublicArticlesAndCommunityAssetsExist()
    {
        using var web = new HttpClient { BaseAddress = new("http://localhost:5200/") };
        var sitemap = System.Xml.Linq.XDocument.Parse(await web.GetStringAsync("sitemap.xml"));
        System.Xml.Linq.XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        Assert.True(sitemap.Descendants(ns + "loc").Count() >= 31);
        Assert.Contains("sitemap.xml", await web.GetStringAsync("robots.txt"));
        Assert.Contains("community.js", await web.GetStringAsync("cong-dong"));
        Assert.Contains("async function community", await web.GetStringAsync("js/community.js"));
    }
}
