using Microsoft.AspNetCore.Mvc.Filters;
using NewsPortal.Api.Data;
using NewsPortal.Api.Models;
using System.Security.Claims;
namespace NewsPortal.Api.Middleware;
public class AuditFilter(ApplicationDbContext db, ILogger<AuditFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || result.Exception != null) return;
        var uid = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var status = result.Result switch {
            Microsoft.AspNetCore.Mvc.ForbidResult => 403,
            Microsoft.AspNetCore.Mvc.ChallengeResult => 401,
            Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult response => response.StatusCode ?? 200,
            _ => 200
        };
        if (status >= 400) {
            if (status is 401 or 403) logger.LogWarning("Access denied {Method} {Path} by {UserId} status {Status}", request.Method, request.Path, uid, status);
            return;
        }
        logger.LogInformation("Mutation {Method} {Path} by {UserId} status {Status}", request.Method, request.Path, uid, status);
        if (uid == null) return;
        db.AuditLogs.Add(new AuditLog { UserId = uid, Action = request.Method, EntityName = context.Controller.GetType().Name,
            EntityId = context.RouteData.Values.GetValueOrDefault("id")?.ToString() ?? "", Description = request.Path.ToString() });
        await db.SaveChangesAsync();
    }
}
