using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1")]
public class CommentsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("articles/{articleId:int}/comments")]
    public async Task<PageDto<CommentDto>> List(int articleId, [FromQuery] PageQuery input)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId && a.Status == ArticleStatus.Published && a.Category.IsActive)) throw new ApiException(404, "Không tìm thấy bài.");
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var q = db.Comments.AsNoTracking().Where(c => c.ArticleId == articleId && (c.Status == CommentStatus.Approved || c.UserId == uid));
        return await Page(q, input);
    }
    [HttpGet("comments/manage"), Authorize(Roles = "Administrator,Moderator")]
    public Task<PageDto<CommentDto>> Manage([FromQuery] PageQuery input, CommentStatus? status) => Page(db.Comments.AsNoTracking().Where(c => status == null || c.Status == status), input);
    private static async Task<PageDto<CommentDto>> Page(IQueryable<Comment> q, PageQuery input) => new(
        await q.OrderByDescending(c => c.Id).Skip((input.PageIndex - 1) * input.PageSize).Take(input.PageSize).Select(c => new CommentDto(c.Id, c.ArticleId, c.UserId, c.User.FullName, c.ParentCommentId, c.Content, c.Status, c.CreatedAt)).ToListAsync(), input.PageIndex, input.PageSize, await q.CountAsync());
    [HttpPost("articles/{articleId:int}/comments"), Authorize]
    public async Task<IActionResult> Create(int articleId, CommentInput input)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId && a.Status == ArticleStatus.Published && a.Category.IsActive && a.AllowComments)) throw new ApiException(400, "Bài viết không cho phép bình luận.");
        if (input.ParentCommentId.HasValue && !await db.Comments.AnyAsync(c => c.Id == input.ParentCommentId && c.ArticleId == articleId && c.Status == CommentStatus.Approved)) throw new ApiException(400, "Bình luận cha không hợp lệ.");
        var c = new Comment { ArticleId = articleId, UserId = ArticleService.UserId(User), Content = input.Content.Trim(), ParentCommentId = input.ParentCommentId };
        db.Comments.Add(c); await db.SaveChangesAsync(); return StatusCode(201, new { c.Id, c.Status });
    }
    [HttpPut("comments/{id:int}"), Authorize]
    public async Task<IActionResult> Update(int id, CommentInput input)
    {
        var c = await db.Comments.FindAsync(id); if (c == null) return NotFound();
        if (c.UserId != ArticleService.UserId(User)) return Forbid();
        c.Content = input.Content.Trim(); c.Status = CommentStatus.Pending; c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return NoContent();
    }
    [HttpPatch("comments/{id:int}/status"), Authorize(Roles = "Administrator,Moderator")]
    public async Task<IActionResult> Moderate(int id, ModerationInput input)
    {
        var c = await db.Comments.FindAsync(id); if (c == null) return NotFound();
        if (input.Status == CommentStatus.Approved && c.Status != CommentStatus.Approved && c.ParentCommentId.HasValue) {
            var parent = await db.Comments.FindAsync(c.ParentCommentId);
            if (parent != null) db.Notifications.Add(new() { UserId = parent.UserId, Title = "Có phản hồi mới", Message = "Bình luận của bạn vừa nhận phản hồi.", Url = "/tin-tuc/" + await db.Articles.Where(a => a.Id == c.ArticleId).Select(a => a.Slug).SingleAsync() });
        }
        c.Status = input.Status; c.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("comments/{id:int}"), Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Comments.FindAsync(id); if (c == null) return NotFound();
        if (c.UserId != ArticleService.UserId(User) && !User.IsInRole("Administrator") && !User.IsInRole("Moderator")) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Comments.Where(x => x.ParentCommentId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentCommentId, (int?)null));
        db.Comments.Remove(c); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
}
