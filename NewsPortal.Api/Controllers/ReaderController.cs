using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1")]
public class ReaderController(ApplicationDbContext db, IMemoryCache memory) : ControllerBase
{
    private Task<bool> Published(int id) => db.Articles.AnyAsync(a => a.Id == id && a.Status == ArticleStatus.Published && a.Category.IsActive);
    [HttpGet("bookmarks"), Authorize] public async Task<PageDto<ArticleListDto>> Bookmarks([FromQuery] PageQuery input)
    { var uid = ArticleService.UserId(User); return await Page(db.Bookmarks.Where(b => b.UserId == uid).OrderByDescending(b => b.CreatedAt).Select(b => b.Article), input); }
    [HttpGet("history"), Authorize] public async Task<PageDto<ArticleListDto>> History([FromQuery] PageQuery input)
    { var uid = ArticleService.UserId(User); return await Page(db.ReadingHistories.Where(b => b.UserId == uid).OrderByDescending(b => b.ReadAt).Select(b => b.Article), input); }
    private static async Task<PageDto<ArticleListDto>> Page(IQueryable<Article> q, PageQuery p)
    {
        q = q.AsNoTracking().Where(a => a.Status == ArticleStatus.Published && a.Category.IsActive);
        return new(await q.Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).Select(ArticleService.Projection).ToListAsync(), p.PageIndex, p.PageSize, await q.CountAsync());
    }
    [HttpPost("bookmarks/{articleId:int}"), Authorize] public async Task<IActionResult> Save(int articleId)
    {
        if (!await Published(articleId)) return NotFound(); var uid = ArticleService.UserId(User);
        if (!await db.Bookmarks.AnyAsync(b => b.ArticleId == articleId && b.UserId == uid)) { db.Bookmarks.Add(new() { ArticleId = articleId, UserId = uid }); await db.SaveChangesAsync(); }
        return NoContent();
    }
    [HttpDelete("bookmarks/{articleId:int}"), Authorize] public async Task<IActionResult> Remove(int articleId)
    { var uid = ArticleService.UserId(User); await db.Bookmarks.Where(b => b.ArticleId == articleId && b.UserId == uid).ExecuteDeleteAsync(); return NoContent(); }
    [HttpGet("articles/{id:int}/reactions")] public async Task<IActionResult> Reactions(int id)
    {
        if (!await Published(id)) return NotFound();
        return Ok(await db.ArticleReactions.Where(r => r.ArticleId == id).GroupBy(r => r.ReactionType).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync());
    }
    [HttpPut("articles/{id:int}/reaction"), Authorize] public async Task<IActionResult> React(int id, ReactionInput input)
    {
        if (!await Published(id)) return NotFound(); var uid = ArticleService.UserId(User);
        var r = await db.ArticleReactions.SingleOrDefaultAsync(r => r.ArticleId == id && r.UserId == uid);
        if (r == null) { r = new() { ArticleId = id, UserId = uid }; db.ArticleReactions.Add(r); }
        r.ReactionType = input.ReactionType; await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("articles/{id:int}/reaction"), Authorize] public async Task<IActionResult> Unreact(int id)
    { var uid = ArticleService.UserId(User); await db.ArticleReactions.Where(r => r.ArticleId == id && r.UserId == uid).ExecuteDeleteAsync(); return NoContent(); }
    [HttpPost("articles/{id:int}/view")] public async Task<IActionResult> ViewArticle(int id)
    {
        if (!await Published(id)) return NotFound();
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var viewer = uid ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var key = $"view:{viewer}:{id}";
        if (!memory.TryGetValue(key, out _)) {
            memory.Set(key, true, TimeSpan.FromMinutes(30));
            await db.Articles.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.ViewCount, a => a.ViewCount + 1));
        }
        if (uid != null) {
            var history = await db.ReadingHistories.SingleOrDefaultAsync(h => h.UserId == uid && h.ArticleId == id);
            if (history == null) db.ReadingHistories.Add(new() { UserId = uid, ArticleId = id }); else history.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
    [HttpGet("notifications"), Authorize] public async Task<IActionResult> Notifications([FromQuery] PageQuery p)
    {
        var uid = ArticleService.UserId(User); var q = db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
        return Ok(new { data = await q.OrderByDescending(n => n.Id).Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).Select(n => new { n.Id, n.Title, n.Message, n.Url, n.IsRead, n.CreatedAt }).ToListAsync(), totalCount = await q.CountAsync(), p.PageIndex, p.PageSize });
    }
    [HttpPatch("notifications/{id:int}/read"), Authorize] public async Task<IActionResult> Read(int id)
    {
        var uid = ArticleService.UserId(User);
        var count = await db.Notifications.Where(n => n.Id == id && n.UserId == uid).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return count == 0 ? NotFound() : NoContent();
    }
}
