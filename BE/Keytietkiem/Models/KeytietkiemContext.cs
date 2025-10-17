using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Models;

public partial class KeytietkiemContext : DbContext
{
    public KeytietkiemContext()
    {
    }

    public KeytietkiemContext(DbContextOptions<KeytietkiemContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<HomeSection> HomeSections { get; set; }

    public virtual DbSet<LoginAttempt> LoginAttempts { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PaymentGateway> PaymentGateways { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<SiteConfig> SiteConfigs { get; set; }

    public virtual DbSet<SmtpSetting> SmtpSettings { get; set; }

    public virtual DbSet<SocialLink> SocialLinks { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Accounts__349DA5A6B3B912D0");

            entity.HasIndex(e => new { e.Email, e.Status }, "IX_Accounts_Email_Status");

            entity.HasIndex(e => new { e.Status, e.LastLoginAt }, "IX_Accounts_Status_Login").IsDescending(false, true);

            entity.HasIndex(e => e.Email, "UX_Accounts_Email").IsUnique();

            entity.HasIndex(e => e.Username, "UX_Accounts_Username").IsUnique();

            entity.Property(e => e.AccountId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.LastLoginAt).HasPrecision(3);
            entity.Property(e => e.LockedUntil).HasPrecision(3);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.TwoFaenabled).HasColumnName("TwoFAEnabled");
            entity.Property(e => e.TwoFamethod)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("TwoFAMethod");
            entity.Property(e => e.TwoFasecret)
                .HasMaxLength(256)
                .HasColumnName("TwoFASecret");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.Username).HasMaxLength(60);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__AuditLog__A17F239896070FEA");

            entity.HasIndex(e => new { e.Action, e.Resource, e.ActorEmail }, "IX_Audit_Filter");

            entity.HasIndex(e => e.OccurredAt, "IX_Audit_Time").IsDescending();

            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ActorEmail).HasMaxLength(254);
            entity.Property(e => e.EntityId).HasMaxLength(64);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PrevHash).HasMaxLength(32);
            entity.Property(e => e.Resource)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ThisHash).HasMaxLength(32);
            entity.Property(e => e.UserAgent).HasMaxLength(200);
        });

        modelBuilder.Entity<HomeSection>(entity =>
        {
            entity.HasKey(e => e.SectionId).HasName("PK__HomeSect__80EF08722FFDF7A8");

            entity.HasIndex(e => e.Order, "UX_HomeSections_Order").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsVisible).HasDefaultValue(true);
            entity.Property(e => e.SectionKey)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(80);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.AttemptId).HasName("PK__LoginAtt__891A68E69EE34A45");

            entity.Property(e => e.AttemptAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.LoginName).HasMaxLength(254);
            entity.Property(e => e.UserAgent).HasMaxLength(200);
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleId).HasName("PK__Modules__2B7477A73609BCFE");

            entity.Property(e => e.ModuleId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ModuleName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__Password__658FEEEA97BB8C72");

            entity.Property(e => e.TokenId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedIp)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.Property(e => e.UsedAt).HasPrecision(3);

            entity.HasOne(d => d.Account).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ResetTokens_Account");
        });

        modelBuilder.Entity<PaymentGateway>(entity =>
        {
            entity.HasKey(e => e.GatewayId).HasName("PK__PaymentG__66BCD8A09F15EEFE");

            entity.Property(e => e.CallbackUrl).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(60);
            entity.Property(e => e.PublicKey).HasMaxLength(2048);
            entity.Property(e => e.SecretEnc).HasMaxLength(2048);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__Permissi__EFA6FB2FF8F175B7");

            entity.Property(e => e.PermissionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.PermissionName).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A09CAFF24");

            entity.HasIndex(e => e.Name, "UX_Roles_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Desc).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(60);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId, e.ModuleId });

            entity.Property(e => e.EffectiveFrom)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.EffectiveTo).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Module).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.ModuleId)
                .HasConstraintName("FK_RolePermissions_Modules");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("FK_RolePermissions_Permissions");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_RolePermissions_Roles");
        });

        modelBuilder.Entity<SiteConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigKey).HasName("PK__SiteConf__4A3067856A57D99D");

            entity.ToTable("SiteConfig");

            entity.Property(e => e.ConfigKey)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Site");
            entity.Property(e => e.ContactAddress).HasMaxLength(200);
            entity.Property(e => e.ContactEmail).HasMaxLength(254);
            entity.Property(e => e.ContactPhone).HasMaxLength(32);
            entity.Property(e => e.FontFamily).HasMaxLength(60);
            entity.Property(e => e.MaintenanceMsg).HasMaxLength(200);
            entity.Property(e => e.PrimaryColor)
                .HasMaxLength(7)
                .IsUnicode(false);
            entity.Property(e => e.SecondaryColor)
                .HasMaxLength(7)
                .IsUnicode(false);
            entity.Property(e => e.SiteName).HasMaxLength(80);
            entity.Property(e => e.Slogan).HasMaxLength(120);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UploadFormats)
                .HasMaxLength(200)
                .HasDefaultValue("jpg,png,webp,svg");
            entity.Property(e => e.UploadMaxMb)
                .HasDefaultValue(10)
                .HasColumnName("UploadMaxMB");
        });

        modelBuilder.Entity<SmtpSetting>(entity =>
        {
            entity.HasKey(e => e.SmtpId).HasName("PK__SmtpSett__F7072912AA60256E");

            entity.Property(e => e.FromAddress).HasMaxLength(254);
            entity.Property(e => e.Host).HasMaxLength(120);
            entity.Property(e => e.PasswordEnc).HasMaxLength(512);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UseTls).HasDefaultValue(true);
            entity.Property(e => e.Username).HasMaxLength(120);
        });

        modelBuilder.Entity<SocialLink>(entity =>
        {
            entity.HasKey(e => e.SocialId).HasName("PK__SocialLi__67CF711A4DB078B0");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Network)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Url).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C1601D4D5");

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AvatarUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(80);
            entity.Property(e => e.Notes).HasMaxLength(300);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Account).WithMany(p => p.Users)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Account");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.AccountId, e.RoleId });

            entity.HasIndex(e => e.RoleId, "IX_UserRoles_Role");

            entity.Property(e => e.EffectiveFrom).HasPrecision(3);
            entity.Property(e => e.EffectiveTo).HasPrecision(3);

            entity.HasOne(d => d.Account).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_UserRoles_Account");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_UserRoles_Role");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
