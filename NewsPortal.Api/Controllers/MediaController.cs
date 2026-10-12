using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;

[ApiController, Route("api/v1/media"), Authorize(Roles = "Administrator,Editor,Journalist")]
public class MediaController(ApplicationDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery p)
    {
        var q = db.Media.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(p.Search)) q = q.Where(m => m.OriginalFileName.Contains(p.Search));
        return Ok(new { data = await q.OrderByDescending(m => m.Id).Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).Select(m => new { m.Id, m.Url, m.OriginalFileName, m.FileSize, m.UploadedBy, m.CreatedAt }).ToListAsync(), totalCount = await q.CountAsync(), p.PageIndex, p.PageSize });
    }
    [HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] MediaUpload input)
    {
        var file = input.File;
        if (file == null || file.Length == 0 || file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Ảnh phải từ 1 byte đến 5 MB." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer); var b = buffer.ToArray();
        var valid = ext switch {
            ".jpg" or ".jpeg" => file.ContentType == "image/jpeg" && b.Length > 3 && b[0] == 255 && b[1] == 216 && b[2] == 255,
            ".png" => file.ContentType == "image/png" && b.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}),
            ".webp" => file.ContentType == "image/webp" && b.Length > 12 && System.Text.Encoding.ASCII.GetString(b,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(b,8,4) == "WEBP",
            _ => false
        };
        if (!valid) return BadRequest(new { message = "Chỉ chấp nhận ảnh JPG, PNG, WebP đúng định dạng và MIME." });
        var name = Guid.NewGuid().ToString("N") + ext;
        var folder = Path.Combine(env.ContentRootPath, "wwwroot", "uploads"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name); await System.IO.File.WriteAllBytesAsync(path, b);
        var m = new NewsPortal.Api.Models.Media { FileName = name, OriginalFileName = Path.GetFileName(file.FileName)[..Math.Min(Path.GetFileName(file.FileName).Length, 255)], Url = "/uploads/" + name, MimeType = file.ContentType, FileSize = file.Length, UploadedBy = ArticleService.UserId(User) };
        try { db.Media.Add(m); await db.SaveChangesAsync(); }
        catch { System.IO.File.Delete(path); throw; }
        return StatusCode(201, new { m.Id, m.Url });
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await db.Media.FindAsync(id); if (m == null) return NotFound();
        if (!ArticleService.IsEditor(User) && m.UploadedBy != ArticleService.UserId(User)) return Forbid();
        if (await db.Articles.AnyAsync(a => a.ThumbnailUrl == m.Url)) return Conflict(new { message = "Ảnh đang được bài viết sử dụng." });
        db.Media.Remove(m); await db.SaveChangesAsync();
        System.IO.File.Delete(Path.Combine(env.ContentRootPath, "wwwroot", "uploads", m.FileName)); return NoContent();
    }
    /// <summary>Download an existing media file as an attachment for editorial users.</summary>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id)
    {
        var media = await db.Media.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        if (media == null || !System.Text.RegularExpressions.Regex.IsMatch(media.FileName, "^[a-f0-9]{32}\\.(jpg|jpeg|png|webp)$")) return NotFound();
        var path = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", media.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, media.MimeType, media.OriginalFileName);
    }
}
public class MediaUpload { public IFormFile? File { get; set; } }
