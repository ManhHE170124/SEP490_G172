// File: DTOs/AuditLogs/AuditLogDtos.cs
using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.AuditLogs
{
    public class AuditChangeDto
    {
        public string FieldPath { get; set; } = null!;
        public string? Before { get; set; }
        public string? After { get; set; }
    }

    public class AuditLogListFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public string? ActorEmail { get; set; }
        public string? ActorRole { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }

    public class AuditLogListItemDto
    {
        public long AuditId { get; set; }
        public DateTime OccurredAt { get; set; }

        public Guid? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public string? ActorRole { get; set; }

        public string? SessionId { get; set; }
        public string? IpAddress { get; set; }

        public string Action { get; set; } = null!;
        public string? EntityType { get; set; }

        public List<AuditChangeDto> Changes { get; set; } = new();
    }

    public class AuditLogDetailDto
    {
        public long AuditId { get; set; }
        public DateTime OccurredAt { get; set; }

        public Guid? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public string? ActorRole { get; set; }

        public string? SessionId { get; set; }
        public string? IpAddress { get; set; }

        public string Action { get; set; } = null!;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }

        public string? BeforeDataJson { get; set; }
        public string? AfterDataJson { get; set; }

        public List<AuditChangeDto> Changes { get; set; } = new();
    }

    public class AuditLogListResponseDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }

        public IReadOnlyList<AuditLogListItemDto> Items { get; set; }
            = Array.Empty<AuditLogListItemDto>();
    }
}
