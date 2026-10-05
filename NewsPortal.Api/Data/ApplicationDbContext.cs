using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Models;

namespace NewsPortal.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ArticleReaction> ArticleReactions => Set<ArticleReaction>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ReadingHistory> ReadingHistories => Set<ReadingHistory>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<NewsletterSubscriber>().HasIndex(s => s.UserId).IsUnique();
        b.Entity<NewsletterSubscriber>().HasIndex(s => s.Email).IsUnique();
        b.Entity<PollVote>().HasKey(v => new { v.PollId, v.UserId });
        b.Entity<PollVote>().HasOne(v => v.Option).WithMany().HasForeignKey(v => v.PollOptionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PollVote>().HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Tag>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Article>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Article>().HasIndex(x => new { x.Status, x.PublishedAt });
        b.Entity<Category>().HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Article>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Article>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Article>().HasOne(x => x.Editor).WithMany().HasForeignKey(x => x.EditorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ArticleTag>().HasKey(x => new { x.ArticleId, x.TagId });
        b.Entity<ArticleTag>().HasOne(x => x.Article).WithMany(x => x.ArticleTags).HasForeignKey(x => x.ArticleId);
        b.Entity<Comment>().HasOne(x => x.ParentComment).WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Comment>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ArticleReaction>().HasIndex(x => new { x.ArticleId, x.UserId }).IsUnique();
        b.Entity<Bookmark>().HasIndex(x => new { x.ArticleId, x.UserId }).IsUnique();
        b.Entity<ReadingHistory>().HasIndex(x => new { x.ArticleId, x.UserId }).IsUnique();
        b.Entity<ArticleReaction>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Bookmark>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReadingHistory>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Media>().HasOne(x => x.Uploader).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ArticleRevision>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditLog>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
