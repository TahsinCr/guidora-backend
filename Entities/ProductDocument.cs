using System;
using Core.Entities;

namespace Entities
{
    public class ProductDocument : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
