/**
 * File: CategoryDtos.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Objects for Category operations. Provide request/response
 *          contracts between API and clients while hiding internal entity structure.
 *
 * DTOs Included:
 *   - CategoryListItemDto  : Lightweight item for listing categories
 *   - CategoryDetailDto    : Full details of a single category
 *   - CategoryCreateDto    : Payload for creating a new category
 *   - CategoryUpdateDto    : Payload for updating an existing category
 *   - CategoryUpsertItem   : Item used in bulk upsert operations
 *   - CategoryBulkUpsertDto: Wrapper for bulk upsert request
 *
 * Properties Overview:
 *   - CategoryId (int)       : Unique identifier
 *   - CategoryCode (string)  : Slug/unique code of category
 *   - CategoryName (string)  : Display name
 *   - Description (string?)  : Optional description
 *   - IsActive (bool)        : Active status
 *   - DisplayOrder (int)     : Ordering index for UI
 *   - ProductCount (int)     : Computed number of products (list view)
 *
 * Usage:
 *   - API request/response shaping for category features
 *   - Bulk import/export and admin management screens
 */
namespace Keytietkiem.DTOs.Products;

public record CategoryListItemDto(
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    bool IsActive,
    int DisplayOrder,
    int ProductCount
);

public record CategoryDetailDto(
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    string? Description,
    bool IsActive,
    int DisplayOrder,
      int ProductCount
);



public record CategoryCreateDto(
    string CategoryCode,
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

public record CategoryUpsertItem(
    string CategoryCode,
    string CategoryName,
    bool IsActive,
    int DisplayOrder,
    string? Description
);

public record CategoryBulkUpsertDto(IReadOnlyList<CategoryUpsertItem> Items);