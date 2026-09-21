using System;
using Core.Entities;

namespace Entities
{
    public class ProductEditProposal : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int ProposedByUserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string ReviewNote { get; set; } = string.Empty;
    }
}
