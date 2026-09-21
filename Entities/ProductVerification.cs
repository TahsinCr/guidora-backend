using System;
using Core.Entities;

namespace Entities
{
    public class ProductVerification : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int VerificationTypeId { get; set; }
        public int? VerifiedByUserId { get; set; }
        public DateTime VerifiedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
