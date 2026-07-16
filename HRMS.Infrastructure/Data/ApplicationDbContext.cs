using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext.
/// Configures ASP.NET Identity, application entities,
/// relationships, constraints and enterprise authentication tables.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        #region Identity Tables

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .IsRequired();

            entity.HasOne(x => x.Organization)
                .WithMany(o => o.Users)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.ToTable("Roles");

            // Seed default application roles automatically using fixed Guids
            entity.HasData(
                new IdentityRole<Guid>
                {
                    Id = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                    Name = "ADMIN",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole<Guid>
                {
                    Id = Guid.Parse("b2c3d4e5-f67a-8b9c-0d1e-2f3a4b5c6d7e"),
                    Name = "HR",
                    NormalizedName = "HR"
                },
                new IdentityRole<Guid>
                {
                    Id = Guid.Parse("c3d4e5f6-7a8b-9c0d-1e2f-3a4b5c6d7e8f"),
                    Name = "EMPLOYEE",
                    NormalizedName = "EMPLOYEE"
                }
            );
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        #endregion

        #region Organization

        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.Address)
                .HasMaxLength(500);

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            entity.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .IsRequired();
        });

        #endregion

        #region Employee

        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Address)
                .HasMaxLength(500);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Organization)
                .WithMany(o => o.Employees)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        #endregion

        #region Refresh Tokens

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.Property(x => x.SessionId)
                .IsRequired();

            entity.HasIndex(x => x.SessionId)
                .IsUnique();

            entity.Property(x => x.Expires)
                .IsRequired();

            entity.Property(x => x.AbsoluteExpiry)
                .IsRequired();

            entity.Property(x => x.Created)
                .IsRequired();

            entity.Property(x => x.LastUsed);

            entity.Property(x => x.Revoked);

            entity.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(128);

            entity.Property(x => x.RevokeReason)
                .HasMaxLength(100);

            entity.Property(x => x.CreatedByIp)
                .HasMaxLength(50);

            entity.Property(x => x.RevokedByIp)
                .HasMaxLength(50);

            entity.Property(x => x.Browser)
                .HasMaxLength(100);

            entity.Property(x => x.Device)
                .HasMaxLength(50);

            entity.Property(x => x.OperatingSystem)
                .HasMaxLength(50);

            entity.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.UserId);
        });

        #endregion

        #region Authentication Audit

        builder.Entity<AuthAuditLog>(entity =>
        {
            entity.ToTable("AuthAuditLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Event)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.OccurredAt)
                .IsRequired();

            entity.Property(x => x.IpAddress)
                .HasMaxLength(50);

            entity.Property(x => x.UserAgent)
                .HasMaxLength(512);

            entity.Property(x => x.Device)
                .HasMaxLength(50);

            entity.Property(x => x.Browser)
                .HasMaxLength(100);

            entity.Property(x => x.Details)
                .HasMaxLength(1000);

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => x.OccurredAt);
        });

        #endregion
    }
}