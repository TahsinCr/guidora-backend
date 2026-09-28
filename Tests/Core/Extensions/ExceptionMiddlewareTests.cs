using System.Text;
using Core.Exceptions;
using Core.Extensions;
using Core.Utilities.Security.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Core.Extensions;

public sealed class ExceptionMiddlewareTests
{
    [Fact]
    public async Task DerivedValidationExceptionsAlsoMapToBadRequest()
    {
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var middleware = new ExceptionMiddleware(
            _ => Task.FromException(new SpecializedValidationException("The input is invalid.")),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(typeof(AuthenticationRequiredException), StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(AuthorizationDeniedException), StatusCodes.Status403Forbidden)]
    public async Task AuthenticationAndAuthorizationFailuresMapToTheirHttpStatusCodes(
        Type exceptionType, int expectedStatusCode)
    {
        Exception exception = exceptionType == typeof(AuthenticationRequiredException)
            ? new AuthenticationRequiredException("Authentication is required.")
            : new AuthorizationDeniedException("Authorization denied.");
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var middleware = new ExceptionMiddleware(_ => Task.FromException(exception),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    }

    [Fact]
    public async Task DatabaseUniqueConflictMapsToHttp409WithoutExposingDatabaseDetails()
    {
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var middleware = new ExceptionMiddleware(_ => throw new DataConflictException("sensitive database detail"),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        response.Position = 0;
        var body = await new StreamReader(response, Encoding.UTF8).ReadToEndAsync();
        Assert.DoesNotContain("sensitive database detail", body);
    }

    [Fact]
    public async Task OversizedRequestMapsToHttp413()
    {
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var middleware = new ExceptionMiddleware(
            _ => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
    }

    private sealed class SpecializedValidationException(string message) : ValidationException(message);
}
