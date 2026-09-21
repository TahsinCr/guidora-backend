using System;
using Core.Entities;

namespace Entities
{
    public class ProductReport : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int ReportedByUserId { get; set; }
        public int ReportReasonId { get; set; }
        public int ReportStatusId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
    }
}
