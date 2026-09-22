using System;
using Core.Entities;

namespace Entities
{
    public class Product : IEntity
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string ShortDescription { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string WebsiteUrl { get; set; } = string.Empty;

        public string LogoUrl { get; set; } = string.Empty;

        public int ProductStatusId { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

    }
}
