using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using NewsPortal.Web.Services;
namespace NewsPortal.Web.Controllers;

[Route("gateway")]
public class ApiProxyController(PortalApiClient api) : Controller
{
    [HttpPost("auth/{actionName:regex(^(login|register)$)}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string actionName)
    {
        using var content = new StreamContent(Request.Body); content.Headers.ContentType = new("application/json");
        using var response = await api.SendAsync(HttpContext, HttpMethod.Post, "api/v1/auth/" + actionName, content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = body };
        using var doc = JsonDocument.Parse(body); var root = doc.RootElement; var user = root.GetProperty("user");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.GetProperty("id").GetString()!), new(ClaimTypes.Name, user.GetProperty("fullName").GetString()!) };
        claims.AddRange(user.GetProperty("roles").EnumerateArray().Select(r => new Claim(ClaimTypes.Role, r.GetString()!)));
        var properties = new AuthenticationProperties { ExpiresUtc = root.GetProperty("expiresAt").GetDateTime(), IsPersistent = false };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = root.GetProperty("token").GetString()! }]);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), properties);
        return Json(user.Clone());
    }
    [HttpPost("auth/logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try { using var response = await api.SendAsync(HttpContext, HttpMethod.Post, "api/v1/auth/logout"); }
        finally { await HttpContext.SignOutAsync(); }
        return NoContent();
    }
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE"), Route("{**path}"), AutoValidateAntiforgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Forward(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.Text.RegularExpressions.Regex.IsMatch(path, "^[a-zA-Z0-9/_-]+$") || path.StartsWith("auth/login") || path.StartsWith("auth/register") || path.StartsWith("auth/logout")) return BadRequest();
        HttpContent? content = null;
        if (Request.HasFormContentType) {
            // MVC's form value provider may already have consumed Request.Body.
            // Forward the parsed form instead of sending an exhausted stream.
            var form = await Request.ReadFormAsync();
            var multipart = new MultipartFormDataContent();
            foreach (var field in form)
                foreach (var value in field.Value) multipart.Add(new StringContent(value ?? ""), field.Key);
            foreach (var file in form.Files) {
                var part = new StreamContent(file.OpenReadStream());
                if (System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(file.ContentType, out var type)) part.Headers.ContentType = type;
                multipart.Add(part, file.Name, file.FileName);
            }
            content = multipart;
        }
        else if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding")) {
            content = new StreamContent(Request.Body);
            if (Request.ContentType != null) content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(Request.ContentType);
        }
        using (content)
        using (var response = await api.SendAsync(HttpContext, new HttpMethod(Request.Method), "api/v1/" + path + Request.QueryString, content)) {
            if (response.StatusCode == HttpStatusCode.Unauthorized) await HttpContext.SignOutAsync();
            Response.StatusCode = (int)response.StatusCode;
            Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            Response.Headers.CacheControl = "no-store";
            await response.Content.CopyToAsync(Response.Body);
            return new EmptyResult();
        }
    }
    [HttpGet("/media/{file}")]
    public async Task<IActionResult> Media(string file)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(file, "^[a-f0-9]{32}\\.(jpg|jpeg|png|webp)$")) return NotFound();
        using var response = await api.SendAsync(HttpContext, HttpMethod.Get, "uploads/" + file);
        if (!response.IsSuccessStatusCode) return NotFound();
        Response.Headers.CacheControl = "public,max-age=86400";
        return File(await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
    }
}
