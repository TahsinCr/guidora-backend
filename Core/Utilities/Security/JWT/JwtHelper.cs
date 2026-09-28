using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Core.Extensions;
using Core.Utilities.Security.Encyption;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Core.Utilities.Security.JWT
{
    public class JwtHelper:ITokenHelper
    {
        public IConfiguration Configuration { get; }
        private readonly TokenOptions _tokenOptions;
        public JwtHelper(IConfiguration configuration)
        {
            Configuration = configuration;
            _tokenOptions = Configuration.GetSection("TokenOptions").Get<TokenOptions>()
                ?? throw new InvalidOperationException("TokenOptions configuration is missing.");
            
        }
        public AccessToken CreateToken(TokenSubject subject, IReadOnlyCollection<string> operationClaims,
            Guid sessionId, int authorizationVersion)
        {
            var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_tokenOptions.AccessTokenExpirationMinutes);
            var securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
            var signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);
            var jwt = CreateJwtSecurityToken(_tokenOptions, subject, signingCredentials, operationClaims, sessionId,
                authorizationVersion, accessTokenExpiration);
            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
            var token = jwtSecurityTokenHandler.WriteToken(jwt);

            return new AccessToken
            {
                Token = token,
                Expiration = accessTokenExpiration,
                SessionId = sessionId
            };

        }

        public JwtSecurityToken CreateJwtSecurityToken(TokenOptions tokenOptions, TokenSubject subject,
            SigningCredentials signingCredentials, IReadOnlyCollection<string> operationClaims, Guid sessionId,
            int authorizationVersion, DateTime accessTokenExpiration)
        {
            var jwt = new JwtSecurityToken(
                issuer:tokenOptions.Issuer,
                audience:tokenOptions.Audience,
                expires:accessTokenExpiration,
                notBefore:DateTime.UtcNow,
                claims: SetClaims(subject, operationClaims, sessionId, authorizationVersion),
                signingCredentials:signingCredentials
            );
            return jwt;
        }

        private IEnumerable<Claim> SetClaims(TokenSubject subject, IReadOnlyCollection<string> operationClaims, Guid sessionId,
            int authorizationVersion)
        {
            var claims = new List<Claim>();
            claims.AddNameIdentifier(subject.Id);
            claims.AddEmail(subject.Email);
            claims.AddName(subject.DisplayName);
            claims.AddRoles(operationClaims.ToArray());
            claims.Add(new Claim("sid", sessionId.ToString()));
            claims.Add(new Claim("av", authorizationVersion.ToString()));
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            
            return claims;
        }
    }
}
