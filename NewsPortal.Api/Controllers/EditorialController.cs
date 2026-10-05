using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1/articles"), Authorize(Roles = "Administrator,Editor,Journalist")]
public class EditorialController(EditorialService service, ArticleService articles, ApplicationDbContext db) : ControllerBase
{
    [HttpPost("{id:int}/review")] public async Task<IActionResult> Review(int id, ReviewInput input) { await service.ReviewAsync(id, input, User); return NoContent(); }
    [HttpGet("{id:int}/revisions")] public async Task<IActionResult> Revisions(int id, [FromQuery] PageQuery input)
    {
        if (!await articles.Visible(User, true).AnyAsync(a => a.Id == id)) return NotFound();
        var q = db.ArticleRevisions.AsNoTracking().Where(r => r.ArticleId == id);
        return Ok(new { data = await q.OrderByDescending(r => r.Id).Skip((input.PageIndex - 1) * input.PageSize).Take(input.PageSize).Select(r => new { r.Id, r.Title, r.Summary, r.Content, r.ChangedBy, r.CreatedAt }).ToListAsync(), totalCount = await q.CountAsync(), input.PageIndex, input.PageSize });
    }
}
