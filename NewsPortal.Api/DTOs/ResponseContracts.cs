using NewsPortal.Api.Models;
namespace NewsPortal.Api.DTOs;

// Named OpenAPI contracts for endpoints that project anonymous read models.
public record ItemsPageDto<T>(IReadOnlyList<T> Data, int TotalCount, int PageIndex, int PageSize);
public record MediaItemDto(int Id, string Url, string OriginalFileName, long FileSize, string UploadedBy, DateTime CreatedAt);
public record MediaCreatedDto(int Id, string Url);
public record CommentCreatedDto(int Id, CommentStatus Status);
public record ReactionCountDto(ReactionType Type, int Count);
public record NotificationDto(int Id, string Title, string Message, string? Url, bool IsRead, DateTime CreatedAt);
public record RevisionDto(int Id, string Title, string Summary, string Content, string ChangedBy, DateTime CreatedAt);
public record SettingDto(string Key, string Value);
public record SubscriptionStateDto(bool Subscribed);
public record SubscriberDto(int Id, string Email, DateTime SubscribedAt);
public record PollOptionDto(int Id, string Text, int Votes);
public record PollDto(int Id, string Question, bool IsActive, int? MyOptionId, IReadOnlyList<PollOptionDto> Options);
public record CategoryCountDto(string Name, int Count);
public record DashboardDto(int Articles, int Published, int Draft, int Pending, int Users, int Comments, long Views, IReadOnlyList<CategoryCountDto> ByCategory);
