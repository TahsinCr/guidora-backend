using FluentValidation;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentValidation.Results;
using Core.Utilities.Security.Authorization;

namespace Core.Extensions
{
    public class ExceptionMiddleware
    {
        private RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception e)
            {
                await HandleExceptionAsync(httpContext, e);
            }
        }

        private Task HandleExceptionAsync(HttpContext httpContext, Exception e)
        {
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            string message = "Internal Server Error";
            IEnumerable<ValidationFailure> errors;
            if (e.GetType() == typeof(ValidationException))
            {
                message = e.Message;
                errors = ((ValidationException)e).Errors;
                httpContext.Response.StatusCode = 400;
                return httpContext.Response.WriteAsync(new ValidationErrorDetails()
                {
                    StatusCode = httpContext.Response.StatusCode,
                    Message = "Validate Error",
                    Errors = errors
                }.ToString());
            }

            if (e is AuthorizationDeniedException)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return httpContext.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = httpContext.Response.StatusCode,
                    Message = e.Message
                }.ToString());
            }

            if (e is AuthenticationRequiredException)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return httpContext.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = httpContext.Response.StatusCode,
                    Message = e.Message
                }.ToString());
            }

            return httpContext.Response.WriteAsync(new ErrorDetails
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = message
            }.ToString());
        }
    }
}
