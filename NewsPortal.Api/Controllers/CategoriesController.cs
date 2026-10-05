using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1/categories")]
public class CategoriesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet] public async Task<PageDto<CategoryDto>> List([FromQuery] PageQuery input, bool all = false)
    {
        var q = db.Categories.AsNoTracking().Where(c => c.IsActive || (all && (User.IsInRole("Administrator") || User.IsInRole("Editor"))));
        if (!string.IsNullOrWhiteSpace(input.Search)) q = q.Where(c => c.Name.Contains(input.Search));
        return new(await q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).Skip((input.PageIndex - 1) * input.PageSize).Take(input.PageSize).Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.ParentId, c.DisplayOrder, c.IsActive)).ToListAsync(), input.PageIndex, input.PageSize, await q.CountAsync());
    }
    [HttpGet("{id:int}")] public async Task<ActionResult<CategoryDto>> Get(int id)
    {
        var c = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && (c.IsActive || User.IsInRole("Administrator") || User.IsInRole("Editor")));
        return c == null ? NotFound() : new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.ParentId, c.DisplayOrder, c.IsActive);
    }
    [HttpPost, Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Create(CategoryInput input)
    { var c = new Category(); await Save(c, input); return CreatedAtAction(nameof(Get), new { id = c.Id }, new { c.Id }); }
    [HttpPut("{id:int}"), Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Update(int id, CategoryInput input)
    { var c = await db.Categories.FindAsync(id) ?? throw new ApiException(404, "Không tìm thấy chuyên mục."); await Save(c, input); return NoContent(); }
    private async Task Save(Category c, CategoryInput input)
    {
        if (await db.Categories.AnyAsync(x => x.Slug == input.Slug && x.Id != c.Id)) throw new ApiException(409, "Slug đã tồn tại.");
        var parent = input.ParentId; var visited = new HashSet<int>();
        while (parent.HasValue) {
            if (parent == c.Id || !visited.Add(parent.Value)) throw new ApiException(400, "Chuyên mục cha tạo vòng lặp.");
            var p = await db.Categories.FindAsync(parent.Value) ?? throw new ApiException(400, "Chuyên mục cha không tồn tại."); parent = p.ParentId;
        }
        c.Name = input.Name.Trim(); c.Slug = input.Slug; c.Description = input.Description; c.ParentId = input.ParentId;
        c.DisplayOrder = input.DisplayOrder; c.IsActive = input.IsActive; c.UpdatedAt = DateTime.UtcNow;
        if (c.Id == 0) db.Categories.Add(c); await db.SaveChangesAsync();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Administrator,Editor")] public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.FindAsync(id); if (c == null) return NotFound();
        if (await db.Articles.AnyAsync(a => a.CategoryId == id) || await db.Categories.AnyAsync(a => a.ParentId == id)) return Conflict(new { message = "Chuyên mục còn bài viết hoặc chuyên mục con." });
        db.Categories.Remove(c); await db.SaveChangesAsync(); return NoContent();
    }
}
