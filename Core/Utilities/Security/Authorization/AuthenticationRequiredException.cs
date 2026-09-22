using System;

namespace Core.Utilities.Security.Authorization
{
    public sealed class AuthenticationRequiredException : Exception
    {
        public AuthenticationRequiredException(string message) : base(message)
        {
        }
    }
}
