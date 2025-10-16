using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SmtpSetting
{
    public int SmtpId { get; set; }

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public string Username { get; set; } = null!;

    public byte[] PasswordEnc { get; set; } = null!;

    public bool UseTls { get; set; }

    public string FromAddress { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
