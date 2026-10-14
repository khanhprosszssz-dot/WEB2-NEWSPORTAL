using Microsoft.AspNetCore.Authentication.Cookies;
using NewsPortal.Web.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.Cookie.Name = "NewsPortal.Session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.LoginPath = "/dang-nhap"; o.ExpireTimeSpan = TimeSpan.FromHours(2); o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddHttpClient<PortalApiClient>(c => { c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7280/"); c.Timeout = TimeSpan.FromSeconds(30); });
var app = builder.Build();
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
// Media Proxy - Lấy ảnh từ NewsPortal.Api
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) ||
        !context.Request.Path.StartsWithSegments("/media", out var remaining))
    {
        await next();
        return;
    }

    var fileName = remaining.Value?.TrimStart('/');

    if (string.IsNullOrWhiteSpace(fileName) ||
        fileName.Contains('/') ||
        fileName.Contains('\\') ||
        fileName == "." ||
        fileName == "..")
    {
        context.Response.StatusCode = 404;
        return;
    }

    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
                     ?? "https://localhost:7280/";

    var imageUrl = new Uri(
        new Uri(apiBaseUrl.TrimEnd('/') + "/"),
        "uploads/" + Uri.EscapeDataString(fileName)
    );

    var factory = context.RequestServices
        .GetRequiredService<IHttpClientFactory>();

    using var client = factory.CreateClient();

    try
    {
        using var response = await client.GetAsync(
            imageUrl,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted
        );

        context.Response.StatusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
            return;

        context.Response.ContentType =
            response.Content.Headers.ContentType?.ToString()
            ?? "application/octet-stream";

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                context.RequestAborted
            );

        await stream.CopyToAsync(
            context.Response.Body,
            context.RequestAborted
        );
    }
    catch (HttpRequestException)
    {
        if (!context.Response.HasStarted)
            context.Response.StatusCode = 502;
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
