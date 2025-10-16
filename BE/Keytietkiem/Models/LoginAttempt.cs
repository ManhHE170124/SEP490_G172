using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class LoginAttempt
{
    public long AttemptId { get; set; }

    public Guid? AccountId { get; set; }

    public string? LoginName { get; set; }

    public DateTime AttemptAt { get; set; }

    public bool Success { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
