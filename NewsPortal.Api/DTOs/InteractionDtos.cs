using System.ComponentModel.DataAnnotations;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.DTOs;
public class CommentInput
{
    [Required, StringLength(2000, MinimumLength = 2)] public string Content { get; set; } = "";
    public int? ParentCommentId { get; set; }
}
public record CommentDto(int Id, int ArticleId, string UserId, string User, int? ParentCommentId, string Content, CommentStatus Status, DateTime CreatedAt);
public class ModerationInput { [EnumDataType(typeof(CommentStatus))] public CommentStatus Status { get; set; } }
public class ReactionInput { [EnumDataType(typeof(ReactionType))] public ReactionType ReactionType { get; set; } }
