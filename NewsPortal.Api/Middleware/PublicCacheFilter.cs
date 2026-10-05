using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Middleware;
public class PublicCacheFilter(PublicCache cache) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var r = context.HttpContext.Request;
        if (!HttpMethods.IsGet(r.Method)) {
            var result = await next();
            if (result.Exception == null) cache.Invalidate();
            return;
        }
        var path = r.Path.Value ?? "";
        if (context.HttpContext.User.Identity?.IsAuthenticated == true || path.Contains("manage") ||
            !(path.StartsWith("/api/v1/categories") || path is "/api/v1/articles/featured" or "/api/v1/articles/popular")) {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            await next(); return;
        }
        var key = cache.Key(path + r.QueryString);
        // ETag revalidation permits immediate invalidation after a mutation; no stale HTTP response cache.
        var etag = "\"" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))) + "\"";
        context.HttpContext.Response.Headers.CacheControl = "public,no-cache,must-revalidate";
        context.HttpContext.Response.Headers.ETag = etag;
        if (cache.TryGet(key, out var value)) {
            context.Result = r.Headers.IfNoneMatch.Contains(etag) ? new StatusCodeResult(304) : new OkObjectResult(value);
            return;
        }
        var executed = await next();
        if (executed.Exception == null && executed.Result is ObjectResult { Value: not null } response && (response.StatusCode ?? 200) == 200)
            cache.Set(key, response.Value);
    }
}
