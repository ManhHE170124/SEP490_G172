using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Products
{
    // Dùng cho danh sách FAQ chung
    public record ProductFaqListItemDto(
        int FaqId,
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive,
        int CategoryCount,   // số Category đang áp dụng (có thể = 0)
        int ProductCount,    // số Product đang áp dụng (có thể = 0)
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // Dùng cho chi tiết FAQ – trả về kèm danh sách Category/Product đang áp dụng
    public record ProductFaqDetailDto(
        int FaqId,
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive,
        IReadOnlyCollection<int> CategoryIds,   // CategoryId (INT)
        IReadOnlyCollection<Guid> ProductIds,   // ProductId (GUID)
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // Tạo mới FAQ – có thể chọn trước Category/Product áp dụng (hoặc để trống)
    public record ProductFaqCreateDto(
        string Question,
        string Answer,
        int SortOrder = 0,
        bool IsActive = true,
        IReadOnlyCollection<int>? CategoryIds = null,
        IReadOnlyCollection<Guid>? ProductIds = null
    );

    // Cập nhật FAQ – overwrite lại danh sách Category/Product
    public record ProductFaqUpdateDto(
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive,
        IReadOnlyCollection<int>? CategoryIds,
        IReadOnlyCollection<Guid>? ProductIds
    );
}
