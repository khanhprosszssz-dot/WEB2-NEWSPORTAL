using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.Services;
public class EditorialService(ApplicationDbContext db, PublicCache cache, ILogger<EditorialService> logger)
{
    public async Task ReviewAsync(int id, ReviewInput input, ClaimsPrincipal user)
    {
        var a = await db.Articles.FindAsync(id) ?? throw new ApiException(404, "Không tìm thấy bài.");
        var uid = ArticleService.UserId(user);
        if (input.Action == "submit") {
            if (a.AuthorId != uid && !ArticleService.IsEditor(user)) throw new ApiException(403, "Không phải bài của bạn.");
            if (a.Status is not (ArticleStatus.Draft or ArticleStatus.Rejected)) throw new ApiException(409, "Chỉ gửi bản nháp hoặc bài bị từ chối.");
            a.Status = ArticleStatus.PendingReview; a.ReviewNote = null;
        } else {
            if (!ArticleService.IsEditor(user)) throw new ApiException(403, "Cần quyền biên tập viên.");
            switch (input.Action) {
                case "approve":
                    if (a.Status != ArticleStatus.PendingReview) throw new ApiException(409, "Bài không ở trạng thái chờ duyệt.");
                    if (input.ScheduledAt.HasValue && input.ScheduledAt <= DateTime.UtcNow) throw new ApiException(400, "Lịch xuất bản phải ở tương lai.");
                    a.Status = ArticleStatus.Approved; a.ScheduledAt = input.ScheduledAt; break;
                case "publish":
                    if (a.Status != ArticleStatus.Approved) throw new ApiException(409, "Cần duyệt bài trước khi xuất bản.");
                    a.Status = ArticleStatus.Published; a.PublishedAt = DateTime.UtcNow; a.ScheduledAt = null; break;
                case "reject":
                    if (a.Status != ArticleStatus.PendingReview) throw new ApiException(409, "Bài không ở trạng thái chờ duyệt.");
                    if (string.IsNullOrWhiteSpace(input.Note)) throw new ApiException(400, "Cần ghi lý do từ chối.");
                    a.Status = ArticleStatus.Rejected; a.ReviewNote = input.Note; break;
                case "archive":
                    if (a.Status != ArticleStatus.Published) throw new ApiException(409, "Chỉ lưu trữ bài đã xuất bản.");
                    a.Status = ArticleStatus.Archived; break;
                default: throw new ApiException(400, "Thao tác không hợp lệ.");
            }
            a.EditorId = uid;
            db.Notifications.Add(new() { UserId = a.AuthorId, Title = "Cập nhật bài viết", Message = a.Title + ": " + a.Status + (input.Note == null ? "" : " — " + input.Note), Url = "/cms?section=articles" });
        }
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); cache.Invalidate(); logger.LogInformation("Article {ArticleId} moved to {Status} by {UserId}", id, a.Status, uid);
    }
}
