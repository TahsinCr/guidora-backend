using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

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
        public ActionResult Login(UserForLoginDto userForLoginDto)
        {
            var userToLogin = _authService.Login(userForLoginDto);
            if (!userToLogin.Success)
            {
                return BadRequest(userToLogin);
            }

            var result = _authService.CreateSession(userToLogin.Data, rememberMe: true, GetDeviceName());
            if (result.Success)
            {
                WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration);
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("register")]
        public ActionResult Register(UserForRegisterDto userForRegisterDto)
        {
            var userExists = _authService.UserExists(userForRegisterDto.Email);
            if (!userExists.Success)
            {
                return BadRequest(userExists);
            }

            var registerResult = _authService.Register(userForRegisterDto,userForRegisterDto.Password);
            if (!registerResult.Success)
            {
                return BadRequest(registerResult);
            }

            var result = _authService.CreateSession(registerResult.Data, rememberMe: true, GetDeviceName());
            if (result.Success)
            {
                WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration);
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("refresh")]
        public ActionResult Refresh()
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

            WriteRefreshTokenCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiration);
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

        private void WriteRefreshTokenCookie(string refreshToken, DateTime expiration)
        {
            Response.Cookies.Append("guidora_refresh", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = new DateTimeOffset(expiration)
            });
        }

        private string GetDeviceName()
        {
            var userAgent = Request.Headers.UserAgent.ToString();
            return userAgent.Length <= 250 ? userAgent : userAgent.Substring(0, 250);
        }
    }
}
