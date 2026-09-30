using Core.Auditing;
using Core.Auditing.Entities;
using Core.DDD.Entities;
using Core.EntityFrameworkCore.Auditing;
using Core.Notifications.Entities;
using Core.OTP.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Core.EntityFrameworkCore.Context;

public class CoreDbContext<TUser, TRole, TKey>(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector) : IdentityDbContext<TUser, TRole, TKey>(options), ICoreDbContext
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserDevice>(builder =>
        {
            builder.Property(x => x.Platform)
                   .HasConversion<string>()
                   .HasMaxLength(100);

            builder.HasIndex(x => x.PushToken)
                .IsUnique();
        });

        modelBuilder.Entity<Otp>(builder =>
        {
            builder.HasIndex(x => x.VerificationId)
                .IsUnique();

            // The column predates email codes; renaming it would be a destructive migration.
            builder.Property(x => x.Recipient)
                .HasColumnName("PhoneNumber")
                .IsRequired();

            builder.Property(x => x.RecipientType)
                .HasConversion<string>()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SequenceNo)
                .ValueGeneratedOnAdd();

            builder.HasIndex(x => x.SequenceNo)
                .IsUnique();

            builder.HasIndex(x => new
            {
                x.UserId,
                x.SequenceNo
            });

            builder.Property(x => x.SourceType)
                   .HasConversion<string>()
                   .HasMaxLength(100);

            builder.ConfigureLocalized(x => x.Title);
            builder.ConfigureLocalized(x => x.Body);

            builder.Property(x => x.Data)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                       v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default))
                   .HasColumnType("jsonb");
        });

        modelBuilder.Entity<NotificationTemplate>(builder =>
        {
            builder.Property(x => x.Id).IsRequired().ValueGeneratedNever();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.ConfigureLocalized(x => x.Title);
            builder.ConfigureLocalized(x => x.Content);
        });

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.Property(x => x.Diff).HasColumnType("jsonb");
            builder.HasIndex(x => x.Timestamp);
            builder.HasIndex(x => new { x.ResourceType, x.Timestamp });
        });

        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Otp>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserDevice>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<NotificationTemplate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DomainEventDispatcher.PublishAsync(mediator, () => ChangeTracker.Entries<Entity>().Select(e => e.Entity), cancellationToken).ConfigureAwait(false);

        var auditedChanges = AuditChangeReader.Read(ChangeTracker);
        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        auditChangeCollector.Record(auditedChanges);
        return result;
    }

    public DbSet<Otp> Otps { get; set; }
    public DbSet<UserDevice> UserDevices { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}