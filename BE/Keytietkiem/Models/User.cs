/**
  File: User.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents user information in the system. Contains personal details,
           contact information, and relationships to accounts, orders, tickets,
           articles, and roles. Supports user management and RBAC.
  Properties:
    - UserId (Guid)           : Unique identifier
    - FirstName (string)      : User's first name
    - LastName (string)       : User's last name
    - FullName (string)       : Complete user name
    - Email (string)          : User's email address (unique)
    - Phone (string)          : Contact phone number
    - Address (string)        : User's address
    - AvatarUrl (string)     : URL to user's avatar image
    - Status (string)         : User status (Active/Inactive)
    - EmailVerified (bool)    : Email verification status
    - CreatedAt (DateTime)    : Account creation timestamp
    - UpdatedAt (DateTime?)   : Last update timestamp
  Relationships:
    - One Account (1:1)
    - Many Articles (1:N)
    - Many Orders (1:N)
    - Many Tickets (1:N)
    - Many TicketReplies (1:N)
    - Many Roles (M:N via UserRoles)
*/

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

    public virtual Account? Account { get; set; }

    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<TicketReply> TicketReplies { get; set; } = new List<TicketReply>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
