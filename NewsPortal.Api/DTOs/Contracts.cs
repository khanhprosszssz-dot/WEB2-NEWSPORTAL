using System.ComponentModel.DataAnnotations;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.DTOs;

public record CreatedResourceDto(int Id);

public record PageDto<T>(IReadOnlyList<T> Data, int PageIndex, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
public class PageQuery
{
    [Range(1, 100000)] public int PageIndex { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 12;
    [StringLength(200)] public string? Search { get; set; }
}
public class ArticleQuery : PageQuery, IValidatableObject
{
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? Author { get; set; }
    public ArticleStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [RegularExpression("^(publishedAt|createdAt|title|viewCount)$")] public string SortColumn { get; set; } = "publishedAt";
    [RegularExpression("^(asc|desc)$")] public string SortOrder { get; set; } = "desc";
    public bool? Featured { get; set; }
    public bool? Breaking { get; set; }
    public bool? EditorsChoice { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FromDate > ToDate) yield return new("Ngày bắt đầu phải trước ngày kết thúc.", [nameof(FromDate)]);
    }
}
public class CategoryInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(160), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    public int? ParentId { get; set; }
    [Range(0, 10000)] public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public record CategoryDto(int Id, string Name, string Slug, string? Description, int? ParentId, int DisplayOrder, bool IsActive);
public class TagInput
{
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, StringLength(100), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
}
public record TagDto(int Id, string Name, string Slug);
public class ArticleInput : IValidatableObject
{
    [Required, StringLength(200, MinimumLength = 5)] public string Title { get; set; } = "";
    [Required, StringLength(220), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
    [StringLength(300)] public string? Subtitle { get; set; }
    [Required, StringLength(1000)] public string Summary { get; set; } = "";
    [Required, StringLength(100000, MinimumLength = 20)] public string Content { get; set; } = "";
    [StringLength(500), RegularExpression("^/uploads/[a-f0-9]{32}\\.(jpg|jpeg|png|webp)$")] public string? ThumbnailUrl { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [MaxLength(20)] public int[] TagIds { get; set; } = [];
    public bool IsFeatured { get; set; }
    public bool IsBreakingNews { get; set; }
    public bool IsEditorsChoice { get; set; }
    public bool AllowComments { get; set; } = true;
    [StringLength(200)] public string? SeoTitle { get; set; }
    [StringLength(300)] public string? SeoDescription { get; set; }
    public byte[]? RowVersion { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Title?.Trim().Length < 5)
            yield return new("Tiêu đề cần ít nhất 5 ký tự sau khi bỏ khoảng trắng ở hai đầu.", [nameof(Title)]);
        if (Content?.Trim().Length < 20)
            yield return new("Nội dung cần ít nhất 20 ký tự sau khi bỏ khoảng trắng ở hai đầu.", [nameof(Content)]);
    }
}
public record ArticleListDto(int Id, string Title, string Slug, string Summary, string? ThumbnailUrl,
    int CategoryId, string Category, string CategorySlug, string AuthorId, string Author, ArticleStatus Status,
    bool IsFeatured, bool IsBreakingNews, bool IsEditorsChoice, int ViewCount, DateTime? PublishedAt, DateTime CreatedAt);
public record ArticleDetailDto(ArticleListDto Article, string? Subtitle, string Content, bool AllowComments,
    string? SeoTitle, string? SeoDescription, string? ReviewNote, DateTime? ScheduledAt, byte[] RowVersion, List<TagDto> Tags);
public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
public class RegisterDto : LoginDto
{
    [Required, StringLength(120)] public string FullName { get; set; } = "";
}
public record UserDto(string Id, string Email, string FullName, string? Bio, string? AvatarUrl, bool IsActive, IList<string> Roles);
public record AuthResponseDto(string Token, DateTime ExpiresAt, UserDto User);
public class ProfileInput
{
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [StringLength(1000)] public string? Bio { get; set; }
}
public class ReviewInput
{
    [Required, RegularExpression("^(submit|approve|reject|publish|archive)$")] public string Action { get; set; } = "";
    [StringLength(1000)] public string? Note { get; set; }
    public DateTime? ScheduledAt { get; set; }
}
