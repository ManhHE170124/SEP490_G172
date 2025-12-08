using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Models;

public partial class KeytietkiemDbContext : DbContext
{
    public KeytietkiemDbContext()
    {
    }

    public KeytietkiemDbContext(DbContextOptions<KeytietkiemDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Badge> Badges { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<LayoutSection> LayoutSections { get; set; }

    public virtual DbSet<LicensePackage> LicensePackages { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationTargetRole> NotificationTargetRoles { get; set; }

    public virtual DbSet<NotificationUser> NotificationUsers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentGateway> PaymentGateways { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostComment> PostComments { get; set; }

    public virtual DbSet<PostType> PostTypes { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductAccount> ProductAccounts { get; set; }

    public virtual DbSet<ProductAccountCustomer> ProductAccountCustomers { get; set; }

    public virtual DbSet<ProductAccountHistory> ProductAccountHistories { get; set; }

    public virtual DbSet<ProductBadge> ProductBadges { get; set; }

    public virtual DbSet<ProductKey> ProductKeys { get; set; }

    public virtual DbSet<ProductReport> ProductReports { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductSection> ProductSections { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<RefundRequest> RefundRequests { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<SlaRule> SlaRules { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupportChatMessage> SupportChatMessages { get; set; }

    public virtual DbSet<SupportChatPriorityWeeklyStat> SupportChatPriorityWeeklyStats { get; set; }

    public virtual DbSet<SupportChatSession> SupportChatSessions { get; set; }

    public virtual DbSet<SupportDailyStat> SupportDailyStats { get; set; }

    public virtual DbSet<SupportPlan> SupportPlans { get; set; }

    public virtual DbSet<SupportPlanMonthlyStat> SupportPlanMonthlyStats { get; set; }

    public virtual DbSet<SupportPriorityLoyaltyRule> SupportPriorityLoyaltyRules { get; set; }

    public virtual DbSet<SupportStaffDailyStat> SupportStaffDailyStats { get; set; }

    public virtual DbSet<SupportTicketSeverityPriorityWeeklyStat> SupportTicketSeverityPriorityWeeklyStats { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketReply> TicketReplies { get; set; }

    public virtual DbSet<TicketSubjectTemplate> TicketSubjectTemplates { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSupportPlanSubscription> UserSupportPlanSubscriptions { get; set; }

    public virtual DbSet<WarrantyClaim> WarrantyClaims { get; set; }

    public virtual DbSet<WebsiteSetting> WebsiteSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK_Accounts");

            entity.ToTable("Account");

            entity.HasIndex(e => e.UserId, "UQ__Accounts__1788CC4DC1BA8C54").IsUnique();

            entity.HasIndex(e => e.Username, "UQ__Accounts__536C85E47EEB9C95").IsUnique();

            entity.Property(e => e.AccountId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LastLoginAt).HasPrecision(3);
            entity.Property(e => e.LockedUntil).HasPrecision(3);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.Username).HasMaxLength(60);

            entity.HasOne(d => d.User).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.UserId)
                .HasConstraintName("FK_Accounts_User");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__AuditLog__A17F2398B6C01443");

            entity.ToTable("AuditLog");

            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ActorEmail).HasMaxLength(254);
            entity.Property(e => e.ActorRole)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EntityId).HasMaxLength(128);
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SessionId).HasMaxLength(100);
        });

        modelBuilder.Entity<Badge>(entity =>
        {
            entity.HasKey(e => e.BadgeCode).HasName("PK__Badges__8BF404E77A8D77E3");

            entity.ToTable("Badge");

            entity.Property(e => e.BadgeCode).HasMaxLength(32);
            entity.Property(e => e.ColorHex)
                .HasMaxLength(9)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisplayName).HasMaxLength(64);
            entity.Property(e => e.Icon).HasMaxLength(64);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B232590E3");

            entity.ToTable("Category");

            entity.HasIndex(e => e.CategoryCode, "UQ__Categori__371BA955DCE4758A").IsUnique();

            entity.Property(e => e.CategoryCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.HasKey(e => e.FaqId).HasName("PK__Faqs__9C741C43FB6254C5");

            entity.ToTable("Faq");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Question).HasMaxLength(500);

            entity.HasMany(d => d.Categories).WithMany(p => p.Faqs)
                .UsingEntity<Dictionary<string, object>>(
                    "FaqCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .HasConstraintName("FK_FaqCategories_Categories"),
                    l => l.HasOne<Faq>().WithMany()
                        .HasForeignKey("FaqId")
                        .HasConstraintName("FK_FaqCategories_Faqs"),
                    j =>
                    {
                        j.HasKey("FaqId", "CategoryId").HasName("PK_FaqCategories");
                        j.ToTable("FaqCategory");
                    });

            entity.HasMany(d => d.Products).WithMany(p => p.Faqs)
                .UsingEntity<Dictionary<string, object>>(
                    "FaqProduct",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .HasConstraintName("FK_FaqProducts_Products"),
                    l => l.HasOne<Faq>().WithMany()
                        .HasForeignKey("FaqId")
                        .HasConstraintName("FK_FaqProducts_Faqs"),
                    j =>
                    {
                        j.HasKey("FaqId", "ProductId").HasName("PK_FaqProducts");
                        j.ToTable("FaqProduct");
                    });
        });

        modelBuilder.Entity<LayoutSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LayoutSe__3214EC071F62B302");

            entity.ToTable("LayoutSection");

            entity.HasIndex(e => e.SectionKey, "UQ_LayoutSections_SectionKey").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SectionKey).HasMaxLength(100);
            entity.Property(e => e.SectionName).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<LicensePackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PK__LicenseP__322035CCAC2FE4F7");

            entity.ToTable("LicensePackage");

            entity.HasIndex(e => e.CreatedAt, "IX_LicensePackages_CreatedAt").IsDescending();

            entity.HasIndex(e => e.SupplierId, "IX_LicensePackages_Supplier");

            entity.Property(e => e.PackageId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Supplier).WithMany(p => p.LicensePackages)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_LicensePackages_Supplier");

            entity.HasOne(d => d.Variant).WithMany(p => p.LicensePackages)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LicensePackages_ProductVariant");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleId).HasName("PK__Modules__2B7477A720AF1A3B");

            entity.ToTable("Module");

            entity.HasIndex(e => e.ModuleName, "UQ__Modules__EAC9AEC3856619C6").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.ModuleName).HasMaxLength(80);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC0728470C4B");

            entity.ToTable("Notification");

            entity.HasIndex(e => e.CreatedAtUtc, "IX_Notification_CreatedAtUtc").IsDescending();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsSystemGenerated).HasDefaultValue(true);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.RelatedEntityId).HasMaxLength(64);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(100);
            entity.Property(e => e.RelatedUrl).HasMaxLength(512);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Notifications).HasForeignKey(d => d.CreatedByUserId);
        });

        modelBuilder.Entity<NotificationTargetRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC071A6CF8FB");

            entity.ToTable("NotificationTargetRole");

            entity.HasIndex(e => new { e.NotificationId, e.RoleId }, "UX_NotificationTargetRole_Notification_Role").IsUnique();

            entity.Property(e => e.RoleId).HasMaxLength(50);

            entity.HasOne(d => d.Notification).WithMany(p => p.NotificationTargetRoles).HasForeignKey(d => d.NotificationId);

            entity.HasOne(d => d.Role).WithMany(p => p.NotificationTargetRoles).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<NotificationUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC074E20D9CE");

            entity.ToTable("NotificationUser");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAtUtc }, "IX_NotificationUser_User_Read").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.NotificationId, e.UserId }, "UX_NotificationUser_Notification_User").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ReadAtUtc).HasPrecision(0);

            entity.HasOne(d => d.Notification).WithMany(p => p.NotificationUsers).HasForeignKey(d => d.NotificationId);

            entity.HasOne(d => d.User).WithMany(p => p.NotificationUsers).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF1DF8D4F5");

            entity.ToTable("Order");

            entity.Property(e => e.OrderId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.FinalAmount)
                .HasComputedColumnSql("([TotalAmount]-[DiscountAmount])", true)
                .HasColumnType("decimal(13, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Orders_User");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D36CD6BB586C");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Key).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.KeyId)
                .HasConstraintName("FK_OrderDetails_Key");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetails_Order");

            entity.HasOne(d => d.Variant).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetails_Variant");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38D28FD774");

            entity.ToTable("Payment");

            entity.Property(e => e.PaymentId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Provider)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PayOS");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.TransactionType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ORDER_PAYMENT");
        });

        modelBuilder.Entity<PaymentGateway>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PaymentG__3214EC07E79718A1");

            entity.ToTable("PaymentGateway");

            entity.Property(e => e.CallbackUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__Permissi__EFA6FB2F887090C6");

            entity.ToTable("Permission");

            entity.HasIndex(e => e.PermissionName, "UQ__Permissi__0FFDA357CFDE1B70").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.PermissionName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__Posts__AA12603818D9187C");

            entity.ToTable("Post");

            entity.HasIndex(e => e.Slug, "UQ__Posts__BC7B5FB6F4753FF9").IsUnique();

            entity.Property(e => e.PostId)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("PostID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.MetaDescription).HasMaxLength(255);
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.PostTypeId).HasColumnName("PostTypeID");
            entity.Property(e => e.ShortDescription).HasMaxLength(500);
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.Thumbnail).HasMaxLength(255);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.ViewCount).HasDefaultValue(0);

            entity.HasOne(d => d.Author).WithMany(p => p.Posts)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("FK__Posts__AuthorID__15DA3E5D");

            entity.HasOne(d => d.PostType).WithMany(p => p.Posts)
                .HasForeignKey(d => d.PostTypeId)
                .HasConstraintName("FK__Posts__PostTypeI__16CE6296");

            entity.HasMany(d => d.Tags).WithMany(p => p.Posts)
                .UsingEntity<Dictionary<string, object>>(
                    "PostTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("FK__PostTags__TagID__18B6AB08"),
                    l => l.HasOne<Post>().WithMany()
                        .HasForeignKey("PostId")
                        .HasConstraintName("FK__PostTags__PostID__17C286CF"),
                    j =>
                    {
                        j.HasKey("PostId", "TagId").HasName("PK__PostTags__7C45AF9C10016B91");
                        j.ToTable("PostTag");
                        j.IndexerProperty<Guid>("PostId").HasColumnName("PostID");
                        j.IndexerProperty<Guid>("TagId").HasColumnName("TagID");
                    });
        });

        modelBuilder.Entity<PostComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK_PostComments");

            entity.ToTable("PostComment");

            entity.HasIndex(e => e.ParentCommentId, "IX_PostComments_ParentCommentId");

            entity.HasIndex(e => e.PostId, "IX_PostComments_PostId");

            entity.Property(e => e.CommentId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsApproved).HasDefaultValue(false);

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("FK_PostComments_ParentComment");

            entity.HasOne(d => d.Post).WithMany(p => p.PostComments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("FK_PostComments_Posts");

            entity.HasOne(d => d.User).WithMany(p => p.PostComments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_PostComments_Users");
        });

        modelBuilder.Entity<PostType>(entity =>
        {
            entity.HasKey(e => e.PostTypeId).HasName("PK__PostType__AB2126102CC3272B");

            entity.ToTable("PostType");

            entity.Property(e => e.PostTypeId)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("PostTypeID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.PostTypeName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD4CDEB1D2");

            entity.ToTable("Product");

            entity.HasIndex(e => e.ProductCode, "UQ_Products_ProductCode").IsUnique();

            entity.HasIndex(e => e.ProductCode, "UQ__Products__2F4E024F833C5527").IsUnique();

            entity.HasIndex(e => e.Slug, "UX_Products_Slug").IsUnique();

            entity.Property(e => e.ProductId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ProductCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductName).HasMaxLength(100);
            entity.Property(e => e.ProductType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Slug)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasMany(d => d.Categories).WithMany(p => p.Products)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .HasConstraintName("FK_ProductCategories_Category"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .HasConstraintName("FK_ProductCategories_Product"),
                    j =>
                    {
                        j.HasKey("ProductId", "CategoryId").HasName("PK_ProductCategories");
                        j.ToTable("ProductCategory");
                    });
        });

        modelBuilder.Entity<ProductAccount>(entity =>
        {
            entity.HasKey(e => e.ProductAccountId).HasName("PK__ProductA__DC9454DB0EC3DE61");

            entity.ToTable("ProductAccount");

            entity.HasIndex(e => e.Status, "IX_ProductAccounts_Status");

            entity.HasIndex(e => e.VariantId, "IX_ProductAccounts_Variant");

            entity.Property(e => e.ProductAccountId).ValueGeneratedNever();
            entity.Property(e => e.AccountEmail).HasMaxLength(254);
            entity.Property(e => e.AccountPassword).HasMaxLength(512);
            entity.Property(e => e.AccountUsername).HasMaxLength(100);
            entity.Property(e => e.CogsPrice).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ExpiryDate).HasPrecision(3);
            entity.Property(e => e.MaxUsers).HasDefaultValue(1);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Variant).WithMany(p => p.ProductAccounts)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductAccounts_Variant");
        });

        modelBuilder.Entity<ProductAccountCustomer>(entity =>
        {
            entity.HasKey(e => e.ProductAccountCustomerId).HasName("PK__ProductA__464798379FE40325");

            entity.ToTable("ProductAccountCustomer");

            entity.HasIndex(e => new { e.ProductAccountId, e.UserId }, "IX_ProductAccountCustomers_Account_User");

            entity.HasIndex(e => e.IsActive, "IX_ProductAccountCustomers_Active");

            entity.HasIndex(e => e.UserId, "IX_ProductAccountCustomers_User");

            entity.Property(e => e.AddedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.RemovedAt).HasPrecision(3);

            entity.HasOne(d => d.Order).WithMany(p => p.ProductAccountCustomers)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_ProductAccountCustomers_Order");

            entity.HasOne(d => d.ProductAccount).WithMany(p => p.ProductAccountCustomers)
                .HasForeignKey(d => d.ProductAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductAccountCustomers_Account");

            entity.HasOne(d => d.User).WithMany(p => p.ProductAccountCustomers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductAccountCustomers_User");
        });

        modelBuilder.Entity<ProductAccountHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__ProductA__4D7B4ABD7003F0E3");

            entity.ToTable("ProductAccountHistory");

            entity.HasIndex(e => e.ProductAccountId, "IX_ProductAccountHistory_Account");

            entity.HasIndex(e => e.ActionAt, "IX_ProductAccountHistory_ActionAt").IsDescending();

            entity.HasIndex(e => e.UserId, "IX_ProductAccountHistory_User");

            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ActionAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.ProductAccount).WithMany(p => p.ProductAccountHistories)
                .HasForeignKey(d => d.ProductAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductAccountHistory_Account");

            entity.HasOne(d => d.User).WithMany(p => p.ProductAccountHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_ProductAccountHistory_User");
        });

        modelBuilder.Entity<ProductBadge>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.Badge }).HasName("PK_ProductBadges");

            entity.ToTable("ProductBadge");

            entity.Property(e => e.Badge).HasMaxLength(32);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.BadgeNavigation).WithMany(p => p.ProductBadges)
                .HasForeignKey(d => d.Badge)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductBadges_Badges");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductBadges)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductBadges_Products");
        });

        modelBuilder.Entity<ProductKey>(entity =>
        {
            entity.HasKey(e => e.KeyId).HasName("PK__ProductK__21F5BE4787E336C1");

            entity.ToTable("ProductKey");

            entity.HasIndex(e => e.SupplierId, "IX_ProductKeys_Supplier");

            entity.HasIndex(e => e.VariantId, "IX_ProductKeys_Variant");

            entity.HasIndex(e => e.KeyString, "UQ__ProductK__BE7B08A9B9C32D63").IsUnique();

            entity.Property(e => e.KeyId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ExpiryDate).HasPrecision(3);
            entity.Property(e => e.ImportedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.KeyString).HasMaxLength(255);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Available");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Individual");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Supplier).WithMany(p => p.ProductKeys)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductKeys_Supplier");

            entity.HasOne(d => d.Variant).WithMany(p => p.ProductKeys)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductKeys_Variant");
        });

        modelBuilder.Entity<ProductReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PKProductR3214EC07A1B2C3D4");

            entity.ToTable("ProductReport");

            entity.HasIndex(e => e.CreatedAt, "IX_ProductReports_CreatedAt").IsDescending();

            entity.HasIndex(e => e.Status, "IX_ProductReports_Status");

            entity.HasIndex(e => e.UserId, "IX_ProductReports_User");

            entity.HasIndex(e => e.ProductVariantId, "IX_ProductReports_Variant");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.ProductAccount).WithMany(p => p.ProductReports)
                .HasForeignKey(d => d.ProductAccountId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductReports_ProductAccount");

            entity.HasOne(d => d.ProductKey).WithMany(p => p.ProductReports)
                .HasForeignKey(d => d.ProductKeyId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductReports_ProductKey");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductReports)
                .HasForeignKey(d => d.ProductVariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReports_ProductVariant");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReports_User");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK_ProductReviews");

            entity.ToTable("ProductReview");

            entity.HasIndex(e => e.Rating, "IX_ProductReviews_Rating");

            entity.HasIndex(e => e.VariantId, "IX_ProductReviews_Variant");

            entity.Property(e => e.ReviewId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Content).HasMaxLength(4000);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(120);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Variant).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.VariantId)
                .HasConstraintName("FK_ProductReviews_Variant");
        });

        modelBuilder.Entity<ProductSection>(entity =>
        {
            entity.HasKey(e => e.SectionId).HasName("PK_ProductSections");

            entity.ToTable("ProductSection");

            entity.HasIndex(e => new { e.VariantId, e.SortOrder }, "IX_ProductSections_Variant");

            entity.Property(e => e.SectionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SectionType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Variant).WithMany(p => p.ProductSections)
                .HasForeignKey(d => d.VariantId)
                .HasConstraintName("FK_ProductSections_Variant");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.VariantId).HasName("PK_ProductVariants");

            entity.ToTable("ProductVariant");

            entity.HasIndex(e => new { e.ProductId, e.Title }, "UX_ProductVariants_Product_Title").IsUnique();

            entity.Property(e => e.VariantId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CogsPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ListPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MetaDescription).HasMaxLength(1000);
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.SellPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.Thumbnail).HasMaxLength(512);
            entity.Property(e => e.Title).HasMaxLength(60);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.VariantCode)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductVariants_Product");
        });

        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasKey(e => e.RefundId).HasName("PK__RefundRe__725AB9208A6D2B16");

            entity.ToTable("RefundRequest");

            entity.Property(e => e.RefundId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AB0D1CBFA");

            entity.ToTable("Role");

            entity.Property(e => e.RoleId).HasMaxLength(50);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(60);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId, e.ModuleId }).HasName("PK_RolePermissions");

            entity.ToTable("RolePermission");

            entity.Property(e => e.RoleId).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Module).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.ModuleId)
                .HasConstraintName("FK_RolePermissions_Module");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("FK_RolePermissions_Perm");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_RolePermissions_Role");
        });

        modelBuilder.Entity<SlaRule>(entity =>
        {
            entity.HasKey(e => e.SlaRuleId).HasName("PK_SlaRules");

            entity.ToTable("SlaRule");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Severity)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B44EA0F678");

            entity.ToTable("Supplier");

            entity.Property(e => e.ContactEmail).HasMaxLength(254);
            entity.Property(e => e.ContactPhone).HasMaxLength(32);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LicenseTerms).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<SupportChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK_SupportChatMessages");

            entity.ToTable("SupportChatMessage");

            entity.HasIndex(e => new { e.ChatSessionId, e.SentAt }, "IX_SupportChatMessages_Session_SentAt");

            entity.Property(e => e.SentAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.ChatSession).WithMany(p => p.SupportChatMessages)
                .HasForeignKey(d => d.ChatSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupportChatMessages_Session");

            entity.HasOne(d => d.Sender).WithMany(p => p.SupportChatMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupportChatMessages_Sender");
        });

        modelBuilder.Entity<SupportChatPriorityWeeklyStat>(entity =>
        {
            entity.HasKey(e => new { e.WeekStartDate, e.PriorityLevel }).HasName("PK_SupportChatPriorityWeeklyStats");

            entity.ToTable("SupportChatPriorityWeeklyStat");

            entity.Property(e => e.AvgDurationMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Duration05Count).HasColumnName("Duration_0_5_Count");
            entity.Property(e => e.Duration1020Count).HasColumnName("Duration_10_20_Count");
            entity.Property(e => e.Duration20plusCount).HasColumnName("Duration_20Plus_Count");
            entity.Property(e => e.Duration510Count).HasColumnName("Duration_5_10_Count");
        });

        modelBuilder.Entity<SupportChatSession>(entity =>
        {
            entity.HasKey(e => e.ChatSessionId).HasName("PK_SupportChatSessions");

            entity.ToTable("SupportChatSession");

            entity.HasIndex(e => new { e.CustomerId, e.Status, e.StartedAt }, "IX_SupportChatSessions_Customer");

            entity.HasIndex(e => new { e.Status, e.PriorityLevel, e.LastMessageAt }, "IX_SupportChatSessions_Queue").IsDescending(false, false, true);

            entity.Property(e => e.ChatSessionId).ValueGeneratedNever();
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.LastMessageAt).HasPrecision(3);
            entity.Property(e => e.LastMessagePreview).HasMaxLength(255);
            entity.Property(e => e.StartedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.AssignedStaff).WithMany(p => p.SupportChatSessionAssignedStaffs)
                .HasForeignKey(d => d.AssignedStaffId)
                .HasConstraintName("FK_SupportChatSessions_Staff");

            entity.HasOne(d => d.Customer).WithMany(p => p.SupportChatSessionCustomers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupportChatSessions_Customer");
        });

        modelBuilder.Entity<SupportDailyStat>(entity =>
        {
            entity.HasKey(e => e.StatDate).HasName("PK__SupportD__255A932C90088A6C");

            entity.ToTable("SupportDailyStat");

            entity.Property(e => e.AvgChatDurationMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgChatFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgChatMessagesPerSession).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgTicketFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgTicketFirstResponseSlaRatio).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.AvgTicketResolutionMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgTicketResolutionSlaRatio).HasColumnType("decimal(10, 4)");
        });

        modelBuilder.Entity<SupportPlan>(entity =>
        {
            entity.HasKey(e => e.SupportPlanId).HasName("PK_SupportPlans");

            entity.ToTable("SupportPlan");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SupportPlanMonthlyStat>(entity =>
        {
            entity.HasKey(e => new { e.YearMonth, e.SupportPlanId }).HasName("PK_SupportPlanMonthlyStats");

            entity.ToTable("SupportPlanMonthlyStat");

            entity.Property(e => e.YearMonth)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.SupportPlanRevenue).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SupportPriorityLoyaltyRule>(entity =>
        {
            entity.HasKey(e => e.RuleId).HasName("PK_SupportPriorityLoyaltyRules");

            entity.ToTable("SupportPriorityLoyaltyRule");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinTotalSpend).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SupportStaffDailyStat>(entity =>
        {
            entity.HasKey(e => new { e.StatDate, e.StaffId }).HasName("PK_SupportStaffDailyStats");

            entity.ToTable("SupportStaffDailyStat");

            entity.Property(e => e.AvgChatDurationMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgChatFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgTicketFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgTicketResolutionMinutes).HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<SupportTicketSeverityPriorityWeeklyStat>(entity =>
        {
            entity.HasKey(e => new { e.WeekStartDate, e.Severity, e.PriorityLevel }).HasName("PK_SupportTicketSeverityPriorityWeeklyStats");

            entity.ToTable("SupportTicketSeverityPriorityWeeklyStat");

            entity.Property(e => e.Severity).HasMaxLength(50);
            entity.Property(e => e.AvgFirstResponseMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AvgResolutionMinutes).HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PK__Tags__657CFA4C9AF77166");

            entity.ToTable("Tag");

            entity.HasIndex(e => e.Slug, "UQ__Tags__BC7B5FB628706194").IsUnique();

            entity.Property(e => e.TagId)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("TagID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Slug).HasMaxLength(150);
            entity.Property(e => e.TagName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PK__Tickets__712CC607D30C124F");

            entity.ToTable("Ticket");

            entity.HasIndex(e => new { e.Status, e.SlaStatus, e.PriorityLevel, e.Severity, e.ResolutionDueAt }, "IX_Tickets_Queue");

            entity.HasIndex(e => e.SlaRuleId, "IX_Tickets_SlaRuleId");

            entity.Property(e => e.TicketId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AssignmentState)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Unassigned");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FirstRespondedAt).HasPrecision(3);
            entity.Property(e => e.FirstResponseDueAt).HasPrecision(3);
            entity.Property(e => e.ResolutionDueAt).HasPrecision(3);
            entity.Property(e => e.ResolvedAt).HasPrecision(3);
            entity.Property(e => e.Severity)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Medium");
            entity.Property(e => e.SlaStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("OK");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("New");
            entity.Property(e => e.Subject).HasMaxLength(120);
            entity.Property(e => e.TicketCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Assignee).WithMany(p => p.TicketAssignees)
                .HasForeignKey(d => d.AssigneeId)
                .HasConstraintName("FK_Tickets_Assignee_User");

            entity.HasOne(d => d.SlaRule).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.SlaRuleId)
                .HasConstraintName("FK_Tickets_SlaRules");

            entity.HasOne(d => d.User).WithMany(p => p.TicketUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tickets_User");
        });

        modelBuilder.Entity<TicketReply>(entity =>
        {
            entity.HasKey(e => e.ReplyId).HasName("PK__TicketRe__C25E46092071F1A6");

            entity.ToTable("TicketReply");

            entity.Property(e => e.SentAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Sender).WithMany(p => p.TicketReplies)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TicketReplies_User");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketReplies)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TicketReplies_Ticket");
        });

        modelBuilder.Entity<TicketSubjectTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateCode).HasName("PK_TicketSubjectTemplates");

            entity.ToTable("TicketSubjectTemplate");

            entity.Property(e => e.TemplateCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Severity)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_Users");

            entity.ToTable("User");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105342FF35DEE").IsUnique();

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.AvatarUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.FirstName).HasMaxLength(80);
            entity.Property(e => e.FullName).HasMaxLength(160);
            entity.Property(e => e.LastName).HasMaxLength(80);
            entity.Property(e => e.Phone).HasMaxLength(32);
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.TotalProductSpend).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("FK_UserRoles_Role"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("FK_UserRoles_User"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId").HasName("PK_UserRoles");
                        j.ToTable("UserRole");
                        j.IndexerProperty<string>("RoleId").HasMaxLength(50);
                    });
        });

        modelBuilder.Entity<UserSupportPlanSubscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("PK_UserSupportPlanSubscriptions");

            entity.ToTable("UserSupportPlanSubscription");

            entity.HasIndex(e => new { e.UserId, e.Status, e.ExpiresAt }, "IX_UserSupportPlanSubscriptions_User_Status");

            entity.Property(e => e.SubscriptionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.StartedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Payment).WithMany(p => p.UserSupportPlanSubscriptions)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("FK_UserSupportPlanSubscriptions_Payment");

            entity.HasOne(d => d.SupportPlan).WithMany(p => p.UserSupportPlanSubscriptions)
                .HasForeignKey(d => d.SupportPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSupportPlanSubscriptions_Plan");

            entity.HasOne(d => d.User).WithMany(p => p.UserSupportPlanSubscriptions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSupportPlanSubscriptions_User");
        });

        modelBuilder.Entity<WarrantyClaim>(entity =>
        {
            entity.HasKey(e => e.ClaimId).HasName("PK__Warranty__EF2E139B3FEE90EA");

            entity.ToTable("WarrantyClaim");

            entity.Property(e => e.ClaimId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<WebsiteSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WebsiteS__3214EC07F2A9F6D3");

            entity.ToTable("WebsiteSetting");

            entity.Property(e => e.AllowedExtensions).HasMaxLength(200);
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Facebook).HasMaxLength(255);
            entity.Property(e => e.FontFamily).HasMaxLength(100);
            entity.Property(e => e.Instagram).HasMaxLength(255);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.MetaDescription).HasMaxLength(1000);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.PrimaryColor).HasMaxLength(20);
            entity.Property(e => e.SecondaryColor).HasMaxLength(20);
            entity.Property(e => e.SiteName).HasMaxLength(255);
            entity.Property(e => e.Slogan).HasMaxLength(500);
            entity.Property(e => e.SmtpHost).HasMaxLength(255);
            entity.Property(e => e.SmtpPassword).HasMaxLength(255);
            entity.Property(e => e.SmtpUsername).HasMaxLength(255);
            entity.Property(e => e.TikTok).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.UploadLimitMb)
                .HasDefaultValue(10)
                .HasColumnName("UploadLimitMB");
            entity.Property(e => e.UseDns)
                .HasDefaultValue(false)
                .HasColumnName("UseDNS");
            entity.Property(e => e.UseTls)
                .HasDefaultValue(true)
                .HasColumnName("UseTLS");
            entity.Property(e => e.Zalo).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
