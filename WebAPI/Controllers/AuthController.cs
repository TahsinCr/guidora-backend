using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController:Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth-login")]
        public async Task<IActionResult> Login(UserForLoginDto userForLoginDto, CancellationToken cancellationToken)
        {
            var userToLogin = await _authService.LoginAsync(userForLoginDto, cancellationToken);
            if (!userToLogin.Success)
            {
                return Unauthorized(userToLogin);
            }

            var result = await _authService.CreateSessionAsync(userToLogin.Data, userForLoginDto.RememberMe, GetDeviceName(), cancellationToken);
            if (result.Success)
            {
                WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration,
                    result.Data.IsPersistentSession);
                return Ok(result);
            }

            return this.ToHttpResult(result);
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth-register")]
        public async Task<IActionResult> Register(UserForRegisterDto userForRegisterDto, CancellationToken cancellationToken)
        {
            var registerResult = await _authService.RegisterAsync(userForRegisterDto, userForRegisterDto.Password, cancellationToken);
            if (!registerResult.Success)
            {
                return this.ToHttpResult(registerResult);
            }

            var result = await _authService.CreateSessionAsync(registerResult.Data, userForRegisterDto.RememberMe, GetDeviceName(), cancellationToken);
            if (result.Success)
            {
                WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration,
                    result.Data.IsPersistentSession);
                return Ok(result);
            }

            return this.ToHttpResult(result);
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("auth-refresh")]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
        {
            if (!Request.Cookies.TryGetValue("guidora_refresh", out var refreshToken))
            {
                return Unauthorized();
            }

            var result = await _authService.RefreshSessionAsync(refreshToken, GetDeviceName(), cancellationToken);
            if (!result.Success)
            {
                Response.Cookies.Delete("guidora_refresh");
                return Unauthorized(result);
            }

            WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration,
                result.Data.IsPersistentSession);
            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<ActionResult> Logout(CancellationToken cancellationToken)
        {
            if (Request.Cookies.TryGetValue("guidora_refresh", out var refreshToken))
            {
                await _authService.RevokeSessionAsync(refreshToken, cancellationToken);
            }

            Response.Cookies.Delete("guidora_refresh");
            return NoContent();
        }

        private void WriteRefreshTokenCookie(string refreshToken, DateTime expiration, bool isPersistent)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps ||
                         HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() != true,
                SameSite = SameSiteMode.Lax
            };
            if (isPersistent)
            {
                cookieOptions.Expires = new DateTimeOffset(expiration);
            }

            Response.Cookies.Append("guidora_refresh", refreshToken, cookieOptions);
        }

        private string GetDeviceName()
        {
            var userAgent = Request.Headers.UserAgent.ToString();
            return userAgent.Length <= 250 ? userAgent : userAgent.Substring(0, 250);
        }
    }
}
