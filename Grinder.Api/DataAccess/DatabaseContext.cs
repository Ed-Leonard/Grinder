using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grinder.Api.DataAccess;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<TaskItem> TaskItems { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<Notification> Notification { get; set; }
    public DbSet<Follow> Follow { get; set; }
    public DbSet<Category> Category { get; set; }
    public DbSet<TaskAttachment> TaskAttachments { get; set; }
    public DbSet<Badge> Badges { get; set; }
    public DbSet<UserBadge> UserBadges { get; set; }
    public DbSet<TaskReaction> TaskReactions { get; set; }
    public DbSet<Reaction> Reactions { get; set; }
    public DbSet<TaskView> TaskViews { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<DirectMessage> DirectMessages { get; set; }
    public DbSet<LiveStream> LiveStreams { get; set; }
    public DbSet<WrappedDismissal> WrappedDismissals { get; set; }

    public DbSet<LiveStreamMessage> LiveStreamMessages { get; set; }

    /// <summary>
    /// Extends the default OnModelCreating method to also convert all emails to lowercase, this can be used to impose any other custom constraints in a similar fashion
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasConversion(
                v => v.ToLower(),
                v => v.ToLower()
            );

        modelBuilder.Entity<User>()
            .HasQueryFilter(u => u.IsVerified || u.CreatedAt > DateTime.UtcNow.AddMinutes(-5));

        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Follower)
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Followed)
            .WithMany()
            .HasForeignKey(f => f.FollowedId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Follow>()
            .HasIndex(f => new { f.FollowerId, f.FollowedId })
            .IsUnique();

        modelBuilder.Entity<TaskAttachment>()
            .HasOne(a => a.Task)
            .WithMany(a => a.TaskAttachments)
            .HasForeignKey(attachment => attachment.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskAttachment>()
            .HasIndex(attachment => attachment.TaskItemId);

        modelBuilder.Entity<TaskAttachment>()
            .HasIndex(attachment => new
            {
                attachment.UserId,
                attachment.TaskItemId,
            });

        modelBuilder.Entity<UserBadge>()
            .HasOne(ub => ub.User)
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBadge>()
            .HasOne(ub => ub.Badge)
            .WithMany()
            .HasForeignKey(ub => ub.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBadge>()
            .HasIndex(ub => new { ub.UserId, ub.BadgeId })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.SelectedBadge)
            .WithMany()
            .HasForeignKey(u => u.SelectedBadgeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Reaction>()
            .HasIndex(a => a.FileName)
            .IsUnique();
        modelBuilder.Entity<TaskReaction>()
            .HasIndex(a => new { a.ReactionId, a.TaskItemId, a.UserId })
            .IsUnique();
        modelBuilder.Entity<TaskView>()
            .HasKey(v => new { v.UserId, v.TaskItemId });

        modelBuilder.Entity<WrappedDismissal>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WrappedDismissal>()
            .HasIndex(d => new { d.UserId, d.Year })
            .IsUnique();

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
                v => v.HasValue ? v.Value.UtcDateTime : (DateTime?)null,
                v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : (DateTimeOffset?)null);

            // NEW: plain DateTime properties lose their Kind through SQLite too
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : (DateTime?)null,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(dateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(nullableDateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }

        var taskFieldListConverter = new ValueConverter<List<TaskField>, string>(
            v => string.Join(',', v),
            v => v.Length == 0
                ? new List<TaskField>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Enum.Parse<TaskField>(s))
                    .ToList());

        modelBuilder.Entity<TaskItem>()
            .Property(t => t.ChangedFields)
            .HasConversion(taskFieldListConverter);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.Sender)
            .WithMany()
            .HasForeignKey(dm => dm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.Receiver)
            .WithMany()
            .HasForeignKey(dm => dm.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(dm => new { dm.ReceiverId, dm.IsRead });

        modelBuilder.Entity<LiveStream>()
            .HasIndex(l => l.RoomName)
            .IsUnique();

        modelBuilder.Entity<LiveStream>()
            .HasOne(l => l.Task)
            .WithMany()
            .HasForeignKey(l => l.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // safeguard on multiple requests at once - attributed to codex
        modelBuilder.Entity<LiveStream>()
            .HasIndex(s => s.UserId)
            .HasDatabaseName("IX_Livestreams_UserId")
            .IsUnique()
            .HasFilter("\"EndedAt\" IS NULL");

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Comment)
            .WithMany()
            .HasForeignKey(n => n.CommentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LiveStreamMessage>()
            .HasOne(m => m.LiveStream)
            .WithMany()
            .HasForeignKey(m => m.LiveStreamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LiveStreamMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LiveStreamMessage>()
            .HasIndex(m => new { m.LiveStreamId, m.SentAt });
    }

}
