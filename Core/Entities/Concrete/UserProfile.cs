using System;
using Core.Entities;

namespace Core.Entities.Concrete
{
    public class UserProfile : IEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
