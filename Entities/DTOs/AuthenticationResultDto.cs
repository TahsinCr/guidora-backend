using System;
using Core.Utilities.Security.JWT;
using System.Text.Json.Serialization;

namespace Entities.DTOs
{
    public class AuthenticationResultDto
    {
        public AccessToken AccessToken { get; set; } = new();
        [JsonIgnore]
        public string RefreshToken { get; set; } = string.Empty;
        public bool IsPersistentSession { get; set; }
        public DateTime RefreshTokenExpiration { get; set; }
    }
}
