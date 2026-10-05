using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1/tags")]
public class TagsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet] public async Task<PageDto<TagDto>> List([FromQuery] PageQuery input)
    {
        var q = db.Tags.AsNoTracking(); if (!string.IsNullOrWhiteSpace(input.Search)) q = q.Where(t => t.Name.Contains(input.Search));
        return new(await q.OrderBy(t => t.Name).ThenBy(t => t.Id).Skip((input.PageIndex - 1) * input.PageSize).Take(input.PageSize).Select(t => new TagDto(t.Id, t.Name, t.Slug)).ToListAsync(), input.PageIndex, input.PageSize, await q.CountAsync());
    }
    [HttpGet("{id:int}")] public async Task<ActionResult<TagDto>> Get(int id)
    { var t = await db.Tags.FindAsync(id); return t == null ? NotFound() : new TagDto(t.Id, t.Name, t.Slug); }
    [HttpPost, Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Create(TagInput input)
    { var t = new Tag(); await Save(t, input); return CreatedAtAction(nameof(Get), new { id = t.Id }, new { t.Id }); }
    [HttpPut("{id:int}"), Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Update(int id, TagInput input)
    { var t = await db.Tags.FindAsync(id) ?? throw new ApiException(404, "Không tìm thấy tag."); await Save(t, input); return NoContent(); }
    private async Task Save(Tag t, TagInput input)
    {
        if (await db.Tags.AnyAsync(x => x.Slug == input.Slug && x.Id != t.Id)) throw new ApiException(409, "Slug đã tồn tại.");
        t.Name = input.Name.Trim(); t.Slug = input.Slug; if (t.Id == 0) db.Tags.Add(t); await db.SaveChangesAsync();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Delete(int id)
    { var t = await db.Tags.FindAsync(id); if (t == null) return NotFound(); db.Tags.Remove(t); await db.SaveChangesAsync(); return NoContent(); }
}
