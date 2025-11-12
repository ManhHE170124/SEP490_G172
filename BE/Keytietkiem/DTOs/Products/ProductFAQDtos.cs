using System;

namespace Keytietkiem.DTOs.Products
{
    // Dùng cho danh sách
    public record ProductFaqListItemDto(
        Guid FaqId,
        Guid ProductId,
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // Dùng cho chi tiết
    public record ProductFaqDetailDto(
        Guid FaqId,
        Guid ProductId,
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // Tạo mới
    public record ProductFaqCreateDto(
        string Question,
        string Answer,
        int SortOrder = 0,
        bool IsActive = true
    );

    // Cập nhật
    public record ProductFaqUpdateDto(
        string Question,
        string Answer,
        int SortOrder,
        bool IsActive
    );
}
