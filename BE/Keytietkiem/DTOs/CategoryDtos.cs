namespace Keytietkiem.DTOs;

public record CategoryListItemDto(
    int CategoryId,
    string CategoryCode,      // = Slug
    string CategoryName,
    bool IsActive,
    int DisplayOrder,
    int ProductCount          // computed
);

public record CategoryDetailDto(
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    string? Description,
    bool IsActive,
    int DisplayOrder
);

public record CategoryCreateDto(
    string CategoryCode,      // server sẽ chuẩn hoá thành slug (lower-kebab)
    string CategoryName,
    string? Description,
    bool IsActive = true,
    int DisplayOrder = 0
);

public record CategoryUpdateDto(
    string CategoryName,
    string? Description,
    bool IsActive,
    int DisplayOrder
);

// Bulk upsert cho màn "Tải danh mục / Lưu danh mục"
public record CategoryUpsertItem(
    string CategoryCode,
    string CategoryName,
    bool IsActive,
    int DisplayOrder,
    string? Description
);

public record CategoryBulkUpsertDto(IReadOnlyList<CategoryUpsertItem> Items);
