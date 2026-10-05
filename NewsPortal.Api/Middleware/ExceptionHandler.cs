using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Middleware;
public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        var status = exception switch {
            ApiException e => e.Status,
            DbUpdateConcurrencyException => 409,
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 547 } } => 409,
            _ => 500
        };
        if (status == 500) logger.LogError(exception, "Request failed {TraceId}", context.TraceIdentifier);
        var message = exception is ApiException ? exception.Message : status == 409 ? "Dữ liệu đã thay đổi hoặc còn liên kết. Hãy tải lại và thử lại." : "Có lỗi hệ thống. Vui lòng thử lại.";
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = message, Extensions = { ["traceId"] = context.TraceIdentifier } }, token);
        return true;
    }
}
