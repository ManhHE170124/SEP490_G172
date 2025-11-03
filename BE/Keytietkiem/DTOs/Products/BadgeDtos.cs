/**
 * File: BadgeDtos.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 30/10/2025
 * Version: 1.1.0
 * Purpose: Data Transfer Objects for Badge operations. Used to transfer badge data
 *          between API and clients while hiding internal entity structure.
 *
 * DTOs Included:
 *   - BadgeListItemDto : Used for listing & showing summary badge info (includes ProductsCount)
 *   - BadgeCreateDto   : Used for creating new badges
 *   - BadgeUpdateDto   : Used for updating existing badges
 *
 * Properties Overview:
 *   - BadgeCode (string)    : Unique code identifier of badge
 *   - DisplayName (string)  : Badge name shown to users
 *   - ColorHex (string?)    : Hex color for UI styling
 *   - Icon (string?)        : Icon resource identifier
 *   - IsActive (bool)       : Visibility status (active/inactive)
 *   - ProductsCount (int)   : Number of products using this badge
 */
namespace Keytietkiem.DTOs.Products;

public record BadgeListItemDto(
    string BadgeCode,
    string DisplayName,
    string? ColorHex,
    string? Icon,
    bool IsActive,
    int ProductsCount
);

public record BadgeCreateDto(
    string BadgeCode,
    string DisplayName,
    string? ColorHex,
    string? Icon,
    bool IsActive
);

public record BadgeUpdateDto(
    string DisplayName,
    string? ColorHex,
    string? Icon,
    bool IsActive
);
