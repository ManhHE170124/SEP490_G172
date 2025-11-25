using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class TicketSubjectTemplate
{
    public string TemplateCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string? Category { get; set; }

    public bool IsActive { get; set; }
}
