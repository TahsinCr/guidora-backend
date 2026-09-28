using System;
using System.Collections.Generic;

namespace Core.Utilities.Security.JWT
{
    public interface ITokenHelper
    {
        AccessToken CreateToken(TokenSubject subject, IReadOnlyCollection<string> operationClaims, Guid sessionId,
            int authorizationVersion);
    }
}
