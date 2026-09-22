using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Utilities.Security.JWT
{
    public class TokenOptions
    {
        public string Audience { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 30;
        public int RememberMeExpirationDays { get; set; } = 90;
        public int SessionAbsoluteExpirationDays { get; set; } = 180;
        public int ClockSkewSeconds { get; set; } = 30;
        public string SecurityKey { get; set; } = string.Empty;
    }
}
