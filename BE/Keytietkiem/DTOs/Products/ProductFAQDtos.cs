// File: DTOs/Products/ProductFaqDtos.cs
using System;

namespace Keytietkiem.DTOs.Products
{
    public record ProductFaqCreateDto(string Question, string Answer, int SortOrder, bool IsActive);
    public record ProductFaqUpdateDto(string Question, string Answer, int SortOrder, bool IsActive);
}
