using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;

[ApiController, Route("api/v1/newsletter"), Authorize]
public class NewsletterController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var uid = ArticleService.UserId(User);
        return Ok(new { subscribed = await db.NewsletterSubscribers.AnyAsync(s => s.UserId == uid) });
    }
    [HttpPost]
    public async Task<IActionResult> Subscribe()
    {
        var uid = ArticleService.UserId(User);
        if (await db.NewsletterSubscribers.AnyAsync(s => s.UserId == uid)) return NoContent();
        var email = await db.Users.Where(u => u.Id == uid).Select(u => u.Email!).SingleAsync();
        db.NewsletterSubscribers.Add(new() { UserId = uid, Email = email });
        await db.SaveChangesAsync(); return StatusCode(201);
    }
    [HttpDelete("me")]
    public async Task<IActionResult> Unsubscribe()
    {
        var uid = ArticleService.UserId(User);
        await db.NewsletterSubscribers.Where(s => s.UserId == uid).ExecuteDeleteAsync(); return NoContent();
    }
    [HttpGet, Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Subscribers([FromQuery] PageQuery p)
    {
        var q = db.NewsletterSubscribers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(p.Search)) q = q.Where(s => s.Email.Contains(p.Search));
        return Ok(new { data = await q.OrderByDescending(s => s.Id).Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).Select(s => new { s.Id, s.Email, s.SubscribedAt }).ToListAsync(), totalCount = await q.CountAsync(), p.PageIndex, p.PageSize });
    }
}

[ApiController, Route("api/v1/polls")]
public class PollsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery p, bool all = false)
    {
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var q = db.Polls.AsNoTracking().Where(x => x.IsActive || (all && User.IsInRole("Administrator")));
        return Ok(new { data = await q.OrderByDescending(x => x.Id).Skip((p.PageIndex - 1) * p.PageSize).Take(p.PageSize).Select(x => new {
            x.Id, x.Question, x.IsActive, myOptionId = db.PollVotes.Where(v => v.PollId == x.Id && v.UserId == uid).Select(v => (int?)v.PollOptionId).FirstOrDefault(),
            options = x.Options.OrderBy(o => o.Id).Select(o => new { o.Id, o.Text, votes = db.PollVotes.Count(v => v.PollOptionId == o.Id) }).ToList()
        }).ToListAsync(), totalCount = await q.CountAsync(), p.PageIndex, p.PageSize });
    }
    [HttpPost, Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(PollInput input)
    {
        var poll = new Poll { Question = input.Question.Trim(), Options = input.Options.Select(o => new PollOption { Text = o.Trim() }).ToList() };
        db.Polls.Add(poll); await db.SaveChangesAsync(); return StatusCode(201, new { poll.Id });
    }
    [HttpPatch("{id:int}"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> State(int id, PollState input)
    {
        var count = await db.Polls.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, input.IsActive));
        return count == 0 ? NotFound() : NoContent();
    }
    [HttpPut("{id:int}/vote"), Authorize]
    public async Task<IActionResult> Vote(int id, VoteInput input)
    {
        if (!await db.PollOptions.AnyAsync(o => o.Id == input.OptionId && o.PollId == id && o.Poll.IsActive)) return BadRequest(new { message = "Khảo sát đã đóng hoặc lựa chọn không hợp lệ." });
        var uid = ArticleService.UserId(User);
        var vote = await db.PollVotes.FindAsync(id, uid);
        if (vote == null) { vote = new() { PollId = id, UserId = uid }; db.PollVotes.Add(vote); }
        vote.PollOptionId = input.OptionId; await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.PollVotes.Where(v => v.PollId == id).ExecuteDeleteAsync();
        var count = await db.Polls.Where(p => p.Id == id).ExecuteDeleteAsync();
        await tx.CommitAsync(); return count == 0 ? NotFound() : NoContent();
    }
}
public class PollInput : IValidatableObject
{
    [Required, StringLength(250)] public string Question { get; set; } = "";
    [Required, MinLength(2), MaxLength(8)] public string[] Options { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Options == null) yield break;
        if (Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 200)) yield return new("Lựa chọn phải dài 1–200 ký tự.", [nameof(Options)]);
        if (Options.Select(o => o?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Options.Length) yield return new("Lựa chọn không được trùng nhau.", [nameof(Options)]);
    }
}
public record PollState(bool IsActive);
public record VoteInput([Range(1, int.MaxValue)] int OptionId);
