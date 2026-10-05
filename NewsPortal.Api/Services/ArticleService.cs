using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.Services;

public class ArticleService(ApplicationDbContext db)
{
    public static readonly Expression<Func<Article, ArticleListDto>> Projection = a => new(
        a.Id, a.Title, a.Slug, a.Summary, a.ThumbnailUrl, a.CategoryId, a.Category.Name, a.Category.Slug,
        a.AuthorId, a.Author.FullName, a.Status, a.IsFeatured, a.IsBreakingNews, a.IsEditorsChoice, a.ViewCount, a.PublishedAt, a.CreatedAt);
    public static bool IsEditor(ClaimsPrincipal user) => user.IsInRole("Administrator") || user.IsInRole("Editor");
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiException(401, "Cần đăng nhập.");
    public IQueryable<Article> Visible(ClaimsPrincipal user, bool management = false)
    {
        var q = db.Articles.AsNoTracking();
        if (!management) return q.Where(a => a.Status == ArticleStatus.Published && a.Category.IsActive);
        if (IsEditor(user)) return q;
        if (user.IsInRole("Journalist")) { var id = UserId(user); return q.Where(a => a.AuthorId == id); }
        throw new ApiException(403, "Không có quyền quản lý bài viết.");
    }
    public async Task<PageDto<ArticleListDto>> ListAsync(ArticleQuery input, ClaimsPrincipal user, bool management = false)
    {
        var q = Visible(user, management);
        if (!string.IsNullOrWhiteSpace(input.Search)) q = q.Where(a => a.Title.Contains(input.Search) || a.Summary.Contains(input.Search) || a.Content.Contains(input.Search));
        if (input.Category != null) q = q.Where(a => a.Category.Slug == input.Category);
        if (input.Tag != null) q = q.Where(a => a.ArticleTags.Any(t => t.Tag.Slug == input.Tag));
        if (input.Author != null) q = q.Where(a => a.AuthorId == input.Author);
        if (input.Status.HasValue) q = q.Where(a => a.Status == input.Status);
        if (input.FromDate.HasValue) q = q.Where(a => a.PublishedAt >= input.FromDate);
        if (input.ToDate.HasValue) q = q.Where(a => a.PublishedAt <= input.ToDate);
        if (input.Featured.HasValue) q = q.Where(a => a.IsFeatured == input.Featured);
        if (input.Breaking.HasValue) q = q.Where(a => a.IsBreakingNews == input.Breaking);
        if (input.EditorsChoice.HasValue) q = q.Where(a => a.IsEditorsChoice == input.EditorsChoice);
        var total = await q.CountAsync();
        var asc = input.SortOrder == "asc";
        IOrderedQueryable<Article> ordered = input.SortColumn switch {
            "title" => asc ? q.OrderBy(a => a.Title) : q.OrderByDescending(a => a.Title),
            "viewCount" => asc ? q.OrderBy(a => a.ViewCount) : q.OrderByDescending(a => a.ViewCount),
            "createdAt" => asc ? q.OrderBy(a => a.CreatedAt) : q.OrderByDescending(a => a.CreatedAt),
            _ => asc ? q.OrderBy(a => a.PublishedAt) : q.OrderByDescending(a => a.PublishedAt)
        };
        var data = await ordered.ThenByDescending(a => a.Id).Skip((input.PageIndex - 1) * input.PageSize).Take(input.PageSize).Select(Projection).ToListAsync();
        return new(data, input.PageIndex, input.PageSize, total);
    }
    public async Task<ArticleDetailDto> DetailAsync(int? id, string? slug, ClaimsPrincipal user, bool management = false)
    {
        var q = Visible(user, management).Where(a => id.HasValue ? a.Id == id : a.Slug == slug);
        var list = await q.Select(Projection).SingleOrDefaultAsync() ?? throw new ApiException(404, "Không tìm thấy bài viết.");
        return await q.Select(a => new ArticleDetailDto(list, a.Subtitle, a.Content, a.AllowComments, a.SeoTitle,
            a.SeoDescription, a.ReviewNote, a.ScheduledAt, a.RowVersion,
            a.ArticleTags.Select(t => new TagDto(t.Tag.Id, t.Tag.Name, t.Tag.Slug)).ToList())).SingleAsync();
    }
    public async Task<int> SaveAsync(int? id, ArticleInput input, ClaimsPrincipal user)
    {
        var uid = UserId(user);
        Article a;
        if (id.HasValue) {
            a = await db.Articles.Include(x => x.ArticleTags).SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Không tìm thấy bài.");
            if (!IsEditor(user) && (a.AuthorId != uid || a.Status is not (ArticleStatus.Draft or ArticleStatus.Rejected))) throw new ApiException(403, "Chỉ sửa bản nháp/bài bị từ chối của mình.");
            if (input.RowVersion == null) throw new ApiException(400, "Cần RowVersion để tránh ghi đè thay đổi.");
            db.Entry(a).Property(x => x.RowVersion).OriginalValue = input.RowVersion;
            db.ArticleRevisions.Add(new() { ArticleId = a.Id, Title = a.Title, Summary = a.Summary, Content = a.Content, ChangedBy = uid });
        } else { a = new() { AuthorId = uid }; db.Articles.Add(a); }
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.IsActive)) throw new ApiException(400, "Chuyên mục không hợp lệ.");
        if (input.ThumbnailUrl != null && !await db.Media.AnyAsync(m => m.Url == input.ThumbnailUrl))
            throw new ApiException(400, "Ảnh không tồn tại trong thư viện. Hãy tải ảnh lên trước khi lưu bài.");
        if (await db.Articles.AnyAsync(x => x.Slug == input.Slug && x.Id != a.Id)) throw new ApiException(409, "Slug đã tồn tại.");
        var tagIds = input.TagIds.Distinct().ToArray();
        if (await db.Tags.CountAsync(t => tagIds.Contains(t.Id)) != tagIds.Length) throw new ApiException(400, "Tag không hợp lệ.");
        a.Title = input.Title.Trim(); a.Slug = input.Slug; a.Summary = input.Summary.Trim(); a.Content = input.Content.Trim();
        a.Subtitle = input.Subtitle; a.CategoryId = input.CategoryId; a.ThumbnailUrl = input.ThumbnailUrl;
        a.AllowComments = input.AllowComments; a.SeoTitle = input.SeoTitle; a.SeoDescription = input.SeoDescription;
        if (IsEditor(user)) { a.IsFeatured = input.IsFeatured; a.IsBreakingNews = input.IsBreakingNews; a.IsEditorsChoice = input.IsEditorsChoice; }
        a.UpdatedAt = DateTime.UtcNow;
        a.ArticleTags.RemoveAll(t => !tagIds.Contains(t.TagId));
        foreach (var tag in tagIds.Where(t => a.ArticleTags.All(x => x.TagId != t))) a.ArticleTags.Add(new() { TagId = tag });
        await db.SaveChangesAsync();
        return a.Id;
    }
    public async Task DeleteAsync(int id, ClaimsPrincipal user)
    {
        var a = await db.Articles.FindAsync(id) ?? throw new ApiException(404, "Không tìm thấy bài.");
        if (!IsEditor(user) && (a.AuthorId != UserId(user) || a.Status != ArticleStatus.Draft)) throw new ApiException(403, "Không có quyền xóa bài.");
        // Remove reply links before deleting the article to avoid restrictive self-FK conflicts.
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Comments.Where(c => c.ArticleId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null));
        db.Articles.Remove(a); await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
