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
using Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Core.Extensions
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception e)
            {
                if (e is DataConflictException or BadHttpRequestException)
                    _logger.LogInformation(e, "Request rejected. TraceId: {TraceId}",
                        httpContext.TraceIdentifier);
                else
                    _logger.LogError(e, "Unhandled request exception. TraceId: {TraceId}",
                        httpContext.TraceIdentifier);
                await HandleExceptionAsync(httpContext, e);
            }
        }

        private Task HandleExceptionAsync(HttpContext httpContext, Exception e)
        {
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            string message = "Internal Server Error";
            IEnumerable<ValidationFailure> errors;
            if (e is ValidationException validationException)
            {
                message = validationException.Message;
                errors = validationException.Errors;
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

            if (e is DataConflictException)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
                return httpContext.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = httpContext.Response.StatusCode,
                    Message = "The request conflicts with an existing record."
                }.ToString());
            }

            if (e is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
            {
                httpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return httpContext.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = httpContext.Response.StatusCode,
                    Message = "Request body is too large."
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
