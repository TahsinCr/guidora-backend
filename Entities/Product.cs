using System;
using Core.Entities;

namespace Entities
{
    public class Product : IEntity
    {
        public int ProductId { get; set; }

        public int CategoryId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public short UnitsInStock { get; set; }

        public decimal UnitPrice { get; set; }

        // Guidora fields. The legacy compatibility fields above remain temporarily
        // so the existing Business and API layers can be migrated safely.
        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string ShortDescription { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string WebsiteUrl { get; set; } = string.Empty;

        public string LogoUrl { get; set; } = string.Empty;

        public int ProductStatusId { get; set; } = 1;

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

    }
}
