using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MagicLinkAuthn.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MagicLinkRequest> MagicLinkRequests => Set<MagicLinkRequest>();

    public DbSet<MagicLinkOutboxMessage> MagicLinkOutboxMessages => Set<MagicLinkOutboxMessage>();

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MagicLinkRequest>(entity =>
        {
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Email).HasMaxLength(256).IsRequired();
            entity.Property(request => request.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(request => request.TokenHash).HasColumnType("bytea").IsRequired();
            entity.Property(request => request.ReturnUrl).HasMaxLength(2048);
            entity.Property(request => request.ProviderMessageId).HasMaxLength(128);
            entity.HasIndex(request => request.TokenHash).IsUnique();
            entity.HasIndex(request => new { request.NormalizedEmail, request.CreatedAt });
            entity.HasIndex(request => request.ExpiresAt);
        });

        builder.Entity<MagicLinkOutboxMessage>(entity =>
        {
            entity.HasKey(message => message.MagicLinkRequestId);
            entity.Property(message => message.ProtectedToken).HasColumnType("bytea").IsRequired();
            entity.HasIndex(message => new { message.NextAttemptAt, message.LeaseExpiresAt });
            entity.HasOne(message => message.MagicLinkRequest)
                .WithOne()
                .HasForeignKey<MagicLinkOutboxMessage>(message => message.MagicLinkRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
