using System.Collections.Generic;
using Core.Entities;

namespace Entities.DTOs
{
    public class CreateProductRequest : IDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public List<int> CategoryIds { get; set; } = new();
    }
}
