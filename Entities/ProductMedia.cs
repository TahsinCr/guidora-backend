using System;
using Core.Entities;

namespace Entities
{
    public class ProductMedia : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string AltText { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
