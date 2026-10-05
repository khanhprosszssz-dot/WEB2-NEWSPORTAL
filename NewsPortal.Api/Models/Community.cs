using System.ComponentModel.DataAnnotations;
namespace NewsPortal.Api.Models;
public class NewsletterSubscriber
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [MaxLength(256)] public string Email { get; set; } = "";
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
public class Poll
{
    public int Id { get; set; }
    [MaxLength(250)] public string Question { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PollOption> Options { get; set; } = [];
}
public class PollOption
{
    public int Id { get; set; }
    public int PollId { get; set; }
    public Poll Poll { get; set; } = null!;
    [MaxLength(200)] public string Text { get; set; } = "";
}
public class PollVote
{
    public int PollId { get; set; }
    public Poll Poll { get; set; } = null!;
    public int PollOptionId { get; set; }
    public PollOption Option { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
}
