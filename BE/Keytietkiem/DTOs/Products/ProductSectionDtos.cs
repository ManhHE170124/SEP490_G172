using System;
using System.Collections.Generic;
using System.Linq;

namespace Keytietkiem.DTOs.Products
{
    // ===== Enum / whitelist cho SectionType =====
    public static class ProductSectionEnums
    {
        // Cố định 3 loại: Chính sách bảo hành, Lưu ý, Chi tiết sản phẩm
        // Lưu dưới dạng code để dễ i18n ở FE: WARRANTY | NOTE | DETAIL
        public static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
        {
            "WARRANTY", "NOTE", "DETAIL"
        };

        public static string Normalize(string? input)
            => string.IsNullOrWhiteSpace(input) ? "" : input.Trim().ToUpperInvariant();

        public static bool IsValid(string? input) => Types.Contains(Normalize(input));
    }

    // ===== Query cho danh sách =====
    public record ProductSectionListQuery(
        string? Q,             // tìm theo Title / Content
        string? Type,          // WARRANTY | NOTE | DETAIL
        bool? Active,          // true | false
        string? Sort = "sort", // sort|title|type|active|created|updated
        string? Dir = "asc",  // asc|desc
        int Page = 1,
        int PageSize = 10
    );

    // ===== Item cho danh sách =====
    public record ProductSectionListItemDto(
        Guid SectionId,
        Guid VariantId,
        string SectionType,
        string Title,
        string Content,
        int SortOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // ===== Chi tiết =====
    public record ProductSectionDetailDto(
        Guid SectionId,
        Guid VariantId,
        string SectionType,
        string Title,
        string Content,
        int SortOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    // ===== Tạo mới =====
    public record ProductSectionCreateDto(
        string SectionType,     // WARRANTY | NOTE | DETAIL
        string Title,
        string Content,
        int? SortOrder = null,
        bool IsActive = true
    );

    // ===== Cập nhật =====
    public record ProductSectionUpdateDto(
        string SectionType,     // WARRANTY | NOTE | DETAIL
        string Title,
        string Content,
        int SortOrder,
        bool IsActive
    );

    // ===== Reorder =====
    public record SectionReorderDto(Guid[] SectionIdsInOrder);
}
