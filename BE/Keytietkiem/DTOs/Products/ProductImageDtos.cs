// File: DTOs/Products/ProductImageDtos.cs
namespace Keytietkiem.DTOs.Products
{
    public record ProductImageCreateByUrlDto(string Url, string? AltText, int? SortOrder, bool? IsPrimary);
    public record ProductImageReorderDto(int[] ImageIdsInOrder);
}
