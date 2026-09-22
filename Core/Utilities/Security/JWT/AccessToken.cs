using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Utilities.Security.JWT
{
    public class AccessToken
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public Guid SessionId { get; set; }

    }
}
