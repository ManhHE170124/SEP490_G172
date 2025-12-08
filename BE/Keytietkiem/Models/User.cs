using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public string Status { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int SupportPriorityLevel { get; set; }

    public decimal TotalProductSpend { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<NotificationUser> NotificationUsers { get; set; } = new List<NotificationUser>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<ProductAccountCustomer> ProductAccountCustomers { get; set; } = new List<ProductAccountCustomer>();

    public virtual ICollection<ProductAccountHistory> ProductAccountHistories { get; set; } = new List<ProductAccountHistory>();

    public virtual ICollection<ProductReport> ProductReports { get; set; } = new List<ProductReport>();

    public virtual ICollection<SupportChatMessage> SupportChatMessages { get; set; } = new List<SupportChatMessage>();

    public virtual ICollection<SupportChatSession> SupportChatSessionAssignedStaffs { get; set; } = new List<SupportChatSession>();

    public virtual ICollection<SupportChatSession> SupportChatSessionCustomers { get; set; } = new List<SupportChatSession>();

    public virtual ICollection<Ticket> TicketAssignees { get; set; } = new List<Ticket>();

    public virtual ICollection<TicketReply> TicketReplies { get; set; } = new List<TicketReply>();

    public virtual ICollection<Ticket> TicketUsers { get; set; } = new List<Ticket>();

    public virtual ICollection<UserSupportPlanSubscription> UserSupportPlanSubscriptions { get; set; } = new List<UserSupportPlanSubscription>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
