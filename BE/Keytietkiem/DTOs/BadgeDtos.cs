namespace Keytietkiem.DTOs;

public record BadgeListItemDto(
    string BadgeCode,
    string DisplayName,
    string? ColorHex,
    string? Icon,
    bool IsActive
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
