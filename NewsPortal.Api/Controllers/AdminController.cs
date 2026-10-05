using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;

[ApiController, Route("api/v1/admin"), Authorize(Roles = "Administrator")]
public class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("dashboard"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Dashboard() => Ok(new {
        articles = await db.Articles.CountAsync(), published = await db.Articles.CountAsync(a => a.Status == ArticleStatus.Published),
        draft = await db.Articles.CountAsync(a => a.Status == ArticleStatus.Draft), pending = await db.Articles.CountAsync(a => a.Status == ArticleStatus.PendingReview),
        users = await db.Users.CountAsync(), comments = await db.Comments.CountAsync(), views = await db.Articles.SumAsync(a => (long)a.ViewCount),
        byCategory = await db.Articles.GroupBy(a => a.Category.Name).Select(g => new { name = g.Key, count = g.Count() }).ToListAsync()
    });
    [HttpGet("users")]
    public async Task<PageDto<UserDto>> List([FromQuery] PageQuery p)
    {
        var q = users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(p.Search)) q = q.Where(u => u.FullName.Contains(p.Search) || u.Email!.Contains(p.Search));
        var count = await q.CountAsync();
        var page = await q.OrderBy(u => u.Email).Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).ToListAsync();
        var ids = page.Select(u => u.Id).ToArray();
        var roles = await (from membership in db.UserRoles
                           join role in db.Roles on membership.RoleId equals role.Id
                           where ids.Contains(membership.UserId)
                           select new { membership.UserId, role.Name }).ToListAsync();
        var result = page.Select(u => new UserDto(u.Id, u.Email!, u.FullName, u.Bio, u.AvatarUrl, u.IsActive,
            roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToList())).ToList();
        return new(result, p.PageIndex, p.PageSize, count);
    }
    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> Detail(string id)
    {
        var u = await users.FindByIdAsync(id);
        return u == null ? NotFound() : new UserDto(u.Id, u.Email!, u.FullName, u.Bio, u.AvatarUrl, u.IsActive, await users.GetRolesAsync(u));
    }
    [HttpPut("users/{id}")]
    public async Task<IActionResult> Update(string id, UserAccessInput input)
    {
        if (id == ArticleService.UserId(User)) return Conflict(new { message = "Không thể tự thay đổi quyền hoặc khóa tài khoản đang dùng." });
        var u = await users.FindByIdAsync(id); if (u == null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var oldRoles = await users.GetRolesAsync(u);
        if (oldRoles.Contains("Administrator") && (!input.IsActive || input.Role != "Administrator") &&
            (await users.GetUsersInRoleAsync("Administrator")).Count(x => x.IsActive) <= 1)
            return Conflict(new { message = "Phải giữ ít nhất một quản trị viên hoạt động." });
        Check(await users.RemoveFromRolesAsync(u, oldRoles));
        Check(await users.AddToRoleAsync(u, input.Role));
        u.IsActive = input.IsActive; Check(await users.UpdateAsync(u));
        Check(await users.UpdateSecurityStampAsync(u));
        await tx.CommitAsync(); return NoContent();
    }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw new ApiException(400, string.Join("; ", result.Errors.Select(e => e.Description))); }
    [HttpGet("settings")]
    public async Task<IActionResult> Settings() => Ok(await db.SiteSettings.AsNoTracking().OrderBy(s => s.Key).Select(s => new { s.Key, s.Value }).ToListAsync());
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> Setting(string key, SettingInput input)
    {
        if (key is not ("SiteName" or "FooterText")) return BadRequest(new { message = "Khóa cấu hình không hợp lệ." });
        var s = await db.SiteSettings.FindAsync(key);
        if (s == null) { s = new() { Key = key }; db.SiteSettings.Add(s); }
        s.Value = input.Value; await db.SaveChangesAsync(); return NoContent();
    }
}
public class UserAccessInput
{
    [Required, RegularExpression("^(Administrator|Editor|Journalist|Moderator|User)$")] public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}
public class SettingInput { [Required, StringLength(1000)] public string Value { get; set; } = ""; }

[ApiController, Route("api/v1/settings")]
public class PublicSettingsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.SiteSettings.AsNoTracking().Where(s => s.Key == "SiteName" || s.Key == "FooterText").Select(s => new { s.Key, s.Value }).ToListAsync());
}
