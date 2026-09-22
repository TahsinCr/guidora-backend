using System;
using Core.Entities;

namespace Entities
{
    public class UserAuthenticationState : IEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AuthorizationVersion { get; set; } = 1;
        public DateTime UpdatedAt { get; set; }
    }
}
