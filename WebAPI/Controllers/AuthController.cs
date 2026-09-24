using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
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
        public IActionResult Login(UserForLoginDto userForLoginDto)
        {
            var userToLogin = _authService.Login(userForLoginDto);
            if (!userToLogin.Success)
            {
                return Unauthorized(userToLogin);
            }

            var result = _authService.CreateSession(userToLogin.Data, userForLoginDto.RememberMe, GetDeviceName());
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
        public IActionResult Register(UserForRegisterDto userForRegisterDto)
        {
            var registerResult = _authService.Register(userForRegisterDto,userForRegisterDto.Password);
            if (!registerResult.Success)
            {
                return this.ToHttpResult(registerResult);
            }

            var result = _authService.CreateSession(registerResult.Data, userForRegisterDto.RememberMe, GetDeviceName());
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
        public IActionResult Refresh()
        {
            if (!Request.Cookies.TryGetValue("guidora_refresh", out var refreshToken))
            {
                return Unauthorized();
            }

            var result = _authService.RefreshSession(refreshToken, GetDeviceName());
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
        public ActionResult Logout()
        {
            if (Request.Cookies.TryGetValue("guidora_refresh", out var refreshToken))
            {
                _authService.RevokeSession(refreshToken);
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
