namespace Keytietkiem.DTOs
{
    public class AuditLogDto
    {
        public long AuditId { get; set; }
        public string OccurredAt { get; set; } = default!; 
        public string? ActorEmail { get; set; }
        public string Action { get; set; } = default!;
        public string Resource { get; set; } = default!;
        public string? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? DetailJson { get; set; }
        public string? CorrelationId { get; set; }
        public bool IntegrityAlert { get; set; }
    }
    public class AuditQueryDto
    {
        public string? Actor { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public DateTime? From { get; set; } 
        public DateTime? To { get; set; }    
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    public record CreateAuditDto(
    string Action,
    string Resource,
    string? EntityId,
    string? DetailJson,
    Guid? CorrelationId,
    Guid? ActorId = null,
    string? ActorEmail = null
);
    public record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
}
