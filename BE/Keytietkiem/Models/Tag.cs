using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Tag
{
    public Guid TagId { get; set; }

    public string TagName { get; set; } = null!;

    public string Slug { get; set; } = null!;
}
