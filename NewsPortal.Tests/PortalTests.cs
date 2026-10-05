using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace NewsPortal.Tests;

// Integration tests against the running development API and real SQL Server.
public class PortalTests
{
    private static HttpClient Client() => new() { BaseAddress = new("http://localhost:5100/api/v1/") };
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static async Task<HttpClient> Login(string role)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "NewsPortal.sln"))) root = root.Parent;
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root!.FullName, ".local", "development.json")));
        var c = Client();
        var r = await Json(await c.PostAsJsonAsync("auth/login", new { email = role + "@newsportal.local", password = config.RootElement.GetProperty("Password").GetString() }));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", r.GetProperty("token").GetString());
        return c;
    }
    [Fact]
    public async Task PublicPagingFilteringSortingAndValidation()
    {
        using var c = Client();
        var r = await Json(await c.GetAsync("articles?pageSize=3&category=cong-nghe&sortColumn=title&sortOrder=asc"));
        Assert.InRange(r.GetProperty("data").GetArrayLength(), 1, 3);
        Assert.All(r.GetProperty("data").EnumerateArray(), a => { Assert.Equal("cong-nghe", a.GetProperty("categorySlug").GetString()); Assert.Equal("Published", a.GetProperty("status").GetString()); });
        var first = r.GetProperty("data")[0];
        var found = await Json(await c.GetAsync("articles?search=" + Uri.EscapeDataString(first.GetProperty("title").GetString()!)));
        Assert.Contains(found.GetProperty("data").EnumerateArray(), a => a.GetProperty("id").GetInt32() == first.GetProperty("id").GetInt32());
        var second = await Json(await c.GetAsync("articles?pageSize=3&pageIndex=2"));
        Assert.Equal(2, second.GetProperty("pageIndex").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("articles?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("articles?sortColumn=invalid")).StatusCode);
    }
    [Fact]
    public async Task AuthenticationAndRolesAreEnforced()
    {
        using var anon = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("articles", new {})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("auth/login", new { email = "missing@example.test", password = "invalid" })).StatusCode);
        using var user = await Login("user");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("articles", new {})).StatusCode);
        using var journalist = await Login("journalist");
        Assert.Equal(HttpStatusCode.BadRequest, (await journalist.PostAsJsonAsync("articles", new {})).StatusCode);
        using var admin = await Login("administrator");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("admin/dashboard")).StatusCode);
    }
    [Fact]
    public async Task ArticleCrudWorkflowConcurrencyAndInteractions()
    {
        using var journalist = await Login("journalist"); using var editor = await Login("editor");
        using var user = await Login("user"); using var moderator = await Login("moderator"); using var anon = Client();
        var cats = await Json(await anon.GetAsync("categories"));
        var slug = "integration-" + Guid.NewGuid().ToString("N");
        var input = new Dictionary<string,object?> { ["title"] = "Bài kiểm thử tự động", ["slug"] = slug, ["summary"] = "Dữ liệu kiểm thử được dọn sau khi chạy.", ["content"] = "Nội dung kiểm thử API với cơ sở dữ liệu SQL Server thật.", ["categoryId"] = cats.GetProperty("data")[0].GetProperty("id").GetInt32(), ["tagIds"] = Array.Empty<int>(), ["allowComments"] = true };
        var created = await journalist.PostAsJsonAsync("articles", input); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetInt32();
        try {
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("articles/"+id)).StatusCode);
            var d = await Json(await journalist.GetAsync("articles/manage/"+id));
            input["rowVersion"] = d.GetProperty("rowVersion").GetString(); input["title"] = "Bài kiểm thử đã cập nhật";
            Assert.Equal(HttpStatusCode.NoContent, (await journalist.PutAsJsonAsync("articles/"+id,input)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await journalist.PutAsJsonAsync("articles/"+id,input)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await journalist.PostAsJsonAsync($"articles/{id}/review",new { action="submit" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await journalist.PostAsJsonAsync($"articles/{id}/review",new { action="approve" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsJsonAsync($"articles/{id}/review",new { action="approve" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsJsonAsync($"articles/{id}/review",new { action="publish" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("articles/slug/"+slug)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("bookmarks/"+id,null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await user.PutAsJsonAsync($"articles/{id}/reaction",new { reactionType="Useful" })).StatusCode);
            var comment=await Json(await user.PostAsJsonAsync($"articles/{id}/comments",new { content="Bình luận kiểm thử" }));
            var cid=comment.GetProperty("id").GetInt32();
            var before=await Json(await anon.GetAsync($"articles/{id}/comments")); Assert.Equal(0,before.GetProperty("totalCount").GetInt32());
            Assert.Equal(HttpStatusCode.NoContent,(await moderator.PatchAsJsonAsync($"comments/{cid}/status",new { status="Approved" })).StatusCode);
            var after=await Json(await anon.GetAsync($"articles/{id}/comments")); Assert.Equal(1,after.GetProperty("totalCount").GetInt32());
            Assert.Equal(HttpStatusCode.NoContent,(await user.PostAsync($"articles/{id}/view",null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,(await user.PostAsync($"articles/{id}/view",null)).StatusCode);
            var article=await Json(await anon.GetAsync("articles/"+id)); Assert.Equal(1,article.GetProperty("article").GetProperty("viewCount").GetInt32());
        } finally { Assert.Equal(HttpStatusCode.NoContent,(await editor.DeleteAsync("articles/"+id)).StatusCode); }
        Assert.Equal(HttpStatusCode.NotFound,(await anon.GetAsync("articles/"+id)).StatusCode);
    }
    [Fact]
    public async Task UploadValidatesContentAndServesImage()
    {
        using var editor=await Login("editor");
        using var invalid=new MultipartFormDataContent();
        var bad=new ByteArrayContent("not an image"u8.ToArray());bad.Headers.ContentType=new("image/png");invalid.Add(bad,"file","bad.png");
        Assert.Equal(HttpStatusCode.BadRequest,(await editor.PostAsync("media",invalid)).StatusCode);
        using var valid=new MultipartFormDataContent();
        var good=new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));good.Headers.ContentType=new("image/png");valid.Add(good,"file","test.png");
        var result=await Json(await editor.PostAsync("media",valid));var id=result.GetProperty("id").GetInt32();
        try { using var http=new HttpClient();Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("http://localhost:5100"+result.GetProperty("url").GetString())).StatusCode); }
        finally { Assert.Equal(HttpStatusCode.NoContent,(await editor.DeleteAsync("media/"+id)).StatusCode); }
    }
    [Fact]
    public async Task SwaggerWebGatewayCsrfAndFeeds()
    {
        using var c=new HttpClient();
        var swagger=await c.GetAsync("http://localhost:5100/swagger/v1/swagger.json");swagger.EnsureSuccessStatusCode();
        var spec=await swagger.Content.ReadFromJsonAsync<JsonElement>();Assert.True(spec.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer",out _));
        var page=await c.GetStringAsync("http://localhost:5200/");Assert.Contains("csrf-token",page);Assert.Contains("cms.js",page);
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("http://localhost:5200/gateway/articles")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("http://localhost:5200/gateway/auth/login",new {})).StatusCode);
        var rss=await c.GetStringAsync("http://localhost:5200/rss");Assert.Contains("<rss",rss);
    }
    [Fact]
    public async Task PublicCacheRevalidatesWithEtag()
    {
        using var c=Client();var response=await c.GetAsync("categories");response.EnsureSuccessStatusCode();
        Assert.NotNull(response.Headers.ETag);
        c.DefaultRequestHeaders.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified,(await c.GetAsync("categories")).StatusCode);
    }
    [Fact]
    public async Task TaxonomyCrudAndCacheInvalidation()
    {
        using var editor=await Login("editor");using var anon=Client();
        var cached=await anon.GetAsync("categories");cached.EnsureSuccessStatusCode();
        var slug="test-"+Guid.NewGuid().ToString("N");
        var category=await Json(await editor.PostAsJsonAsync("categories",new { name="Chuyên mục kiểm thử",slug }));
        var cid=category.GetProperty("id").GetInt32();
        var tag=await Json(await editor.PostAsJsonAsync("tags",new { name="Tag kiểm thử",slug }));
        var tid=tag.GetProperty("id").GetInt32();
        try {
            anon.DefaultRequestHeaders.IfNoneMatch.Add(cached.Headers.ETag!);
            Assert.Equal(HttpStatusCode.OK,(await anon.GetAsync("categories")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await editor.PutAsJsonAsync("categories/"+cid,new { name="Vòng lặp",slug,parentId=cid })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,(await editor.PutAsJsonAsync("categories/"+cid,new { name="Đã sửa",slug,displayOrder=5 })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,(await editor.PutAsJsonAsync("tags/"+tid,new { name="Tag đã sửa",slug })).StatusCode);
            var detail=await Json(await anon.GetAsync("categories/"+cid));Assert.Equal("Đã sửa",detail.GetProperty("name").GetString());
        } finally {
            Assert.Equal(HttpStatusCode.NoContent,(await editor.DeleteAsync("tags/"+tid)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,(await editor.DeleteAsync("categories/"+cid)).StatusCode);
        }
    }
    [Fact]
    public async Task ScheduledArticleBecomesPublic()
    {
        using var editor=await Login("editor");using var anon=Client();
        var categories=await Json(await anon.GetAsync("categories"));
        var created=await Json(await editor.PostAsJsonAsync("articles",new { title="Bài kiểm thử lịch xuất bản",slug="schedule-"+Guid.NewGuid().ToString("N"),summary="Kiểm thử worker",content="Nội dung kiểm thử xuất bản tự động theo lịch đã đặt.",categoryId=categories.GetProperty("data")[0].GetProperty("id").GetInt32() }));
        var id=created.GetProperty("id").GetInt32();
        try {
            (await editor.PostAsJsonAsync($"articles/{id}/review",new { action="submit" })).EnsureSuccessStatusCode();
            (await editor.PostAsJsonAsync($"articles/{id}/review",new { action="approve",scheduledAt=DateTime.UtcNow.AddSeconds(2) })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound,(await anon.GetAsync("articles/"+id)).StatusCode);
            var deadline=DateTime.UtcNow.AddSeconds(40);HttpResponseMessage response;
            do { await Task.Delay(1000);response=await anon.GetAsync("articles/"+id); } while(response.StatusCode==HttpStatusCode.NotFound&&DateTime.UtcNow<deadline);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        } finally { (await editor.DeleteAsync("articles/"+id)).EnsureSuccessStatusCode(); }
    }
}
