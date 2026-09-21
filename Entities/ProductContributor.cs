using System;
using Core.Entities;

namespace Entities
{
    public class ProductContributor : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public string ContributionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
