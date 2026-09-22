using System;

namespace Core.Utilities.Security.Authorization
{
    public sealed class AuthorizationDeniedException : Exception
    {
        public AuthorizationDeniedException(string message) : base(message)
        {
        }
    }
}
