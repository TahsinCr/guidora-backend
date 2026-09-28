using System;

namespace Entities.DTOs;

public sealed record ProductListItemDto(
    int ProductId, string Name, string Slug, string ShortDescription,
    string WebsiteUrl, string LogoUrl, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static ProductListItemDto FromProduct(Product product) => new(
        product.ProductId, product.Name, product.Slug, product.ShortDescription,
        product.WebsiteUrl, product.LogoUrl, product.CreatedAt, product.UpdatedAt);
}
