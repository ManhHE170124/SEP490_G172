/**
  File: Account.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents user authentication credentials and security information.
           Contains login details, password hash, security features like account
           locking, and tracks login attempts for security purposes.
  Properties:
    - AccountId (Guid)           : Unique identifier
    - Username (string)          : Login username (unique)
    - PasswordHash (byte[])       : Hashed password for security
    - LastLoginAt (DateTime?)     : Last successful login timestamp
    - FailedLoginCount (int)      : Number of consecutive failed login attempts
    - LockedUntil (DateTime?)    : Account lock expiration timestamp
    - CreatedAt (DateTime)        : Account creation timestamp
    - UpdatedAt (DateTime?)       : Last update timestamp
    - UserId (Guid)              : Foreign key to User table
  Relationships:
    - One User (1:1 via UserId)
  Security Features:
    - Password hashing
    - Account locking mechanism
    - Failed login attempt tracking
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Account
{
    public Guid AccountId { get; set; }

    public string Username { get; set; } = null!;

    public byte[]? PasswordHash { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;
}
