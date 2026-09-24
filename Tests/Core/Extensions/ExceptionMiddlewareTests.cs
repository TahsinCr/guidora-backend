using System.Text;
using Core.Exceptions;
using Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Core.Extensions;

public sealed class ExceptionMiddlewareTests
{
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
}
