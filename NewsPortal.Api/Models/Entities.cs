using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace NewsPortal.Api.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(500)] public string? AvatarUrl { get; set; }
    [MaxLength(1000)] public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}
public class Category
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(160)] public string Slug { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
public enum ArticleStatus { Draft, PendingReview, Approved, Published, Rejected, Archived }
public class Article
{
    public int Id { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(220)] public string Slug { get; set; } = "";
    [MaxLength(300)] public string? Subtitle { get; set; }
    [MaxLength(1000)] public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    [MaxLength(500)] public string? ThumbnailUrl { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string AuthorId { get; set; } = "";
    public ApplicationUser Author { get; set; } = null!;
    public string? EditorId { get; set; }
    public ApplicationUser? Editor { get; set; }
    public ArticleStatus Status { get; set; }
    [MaxLength(30)] public string ArticleType { get; set; } = "News";
    public bool IsFeatured { get; set; }
    public bool IsBreakingNews { get; set; }
    public bool IsEditorsChoice { get; set; }
    public bool AllowComments { get; set; } = true;
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [MaxLength(200)] public string? SeoTitle { get; set; }
    [MaxLength(300)] public string? SeoDescription { get; set; }
    [MaxLength(1000)] public string? ReviewNote { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public List<ArticleTag> ArticleTags { get; set; } = [];
}
public class Tag
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Slug { get; set; } = "";
}
public class ArticleTag
{
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
public enum CommentStatus { Pending, Approved, Rejected, Hidden }
public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    [MaxLength(2000)] public string Content { get; set; } = "";
    public CommentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
public enum ReactionType { Like, Love, Useful, Wow }
public class ArticleReaction
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public ReactionType ReactionType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Bookmark
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ReadingHistory
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
public class Media
{
    public int Id { get; set; }
    [MaxLength(100)] public string FileName { get; set; } = "";
    [MaxLength(255)] public string OriginalFileName { get; set; } = "";
    [MaxLength(500)] public string Url { get; set; } = "";
    [MaxLength(100)] public string MimeType { get; set; } = "";
    public long FileSize { get; set; }
    [MaxLength(250)] public string? AltText { get; set; }
    [MaxLength(500)] public string? Caption { get; set; }
    public string UploadedBy { get; set; } = "";
    public ApplicationUser Uploader { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ArticleRevision
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string ChangedBy { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string Message { get; set; } = "";
    [MaxLength(500)] public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class AuditLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    [MaxLength(100)] public string Action { get; set; } = "";
    [MaxLength(100)] public string EntityName { get; set; } = "";
    [MaxLength(100)] public string EntityId { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class SiteSetting
{
    [Key, MaxLength(80)] public string Key { get; set; } = "";
    [MaxLength(1000)] public string Value { get; set; } = "";
}
