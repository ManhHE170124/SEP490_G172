// File: Services/Interfaces/SystemNotificationCreateRequest.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Interfaces
{
    /// <summary>
    /// Request tạo notification "hệ thống" (không phải manual do Admin tạo).
    /// Dữ liệu nên là tiếng Việt để FE không phải map lại.
    /// </summary>
    public class SystemNotificationCreateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 0..3 (0=Info, 1=Success, 2=Warning, 3=Error) - tuỳ hệ thống bạn đang dùng.
        /// </summary>
        public byte Severity { get; set; } = 0;

        /// <summary>
        /// UserId (người thực hiện) nếu có.
        /// </summary>
        public Guid? CreatedByUserId { get; set; }

        /// <summary>
        /// Email (người thực hiện) nếu có.
        /// </summary>
        public string? CreatedByEmail { get; set; }

        /// <summary>
        /// Optional: phân loại (VD: "Key.ImportCsv", "Product.ReportCreated"...).
        /// </summary>
        public string? Type { get; set; }

        public string? CorrelationId { get; set; }
        public string? DedupKey { get; set; }
        public string? PayloadJson { get; set; }

        /// <summary>
        /// ✅ Link FE để click mở nhanh
        /// </summary>
        public string? RelatedUrl { get; set; }

        /// <summary>
        /// ✅ (DB có) loại entity liên quan (VD: "Ticket", "ProductAccount", "ProductReport"...)
        /// </summary>
        public string? RelatedEntityType { get; set; }

        /// <summary>
        /// ✅ (DB có) id entity liên quan (GUID/string)
        /// </summary>
        public string? RelatedEntityId { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>
        /// Gửi theo role code/role id (hệ thống sẽ tự resolve).
        /// Ví dụ: RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF...
        /// </summary>
        public List<string> TargetRoleCodes { get; set; } = new();

        /// <summary>
        /// Gửi tới user cụ thể (optional).
        /// </summary>
        public List<Guid> TargetUserIds { get; set; } = new();
    }

    public interface INotificationSystemService
    {
        /// <summary>
        /// Tạo & dispatch system notification: có thể gửi theo role, theo user, hoặc cả 2.
        /// Hàm phải "best-effort": không throw làm hỏng luồng chính.
        /// </summary>
        Task<int> CreateAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default);

        Task<int> CreateForRoleCodesAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default);
        Task<int> CreateForUserIdsAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default);
    }
}
