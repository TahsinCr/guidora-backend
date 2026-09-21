using System;
using Core.Entities;

namespace Entities
{
    public class ProductChangelog : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
