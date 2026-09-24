using Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Utilities;
using BusinessNotFoundResult = Core.Utilities.Results.NotFoundResult;
using BusinessConflictResult = Core.Utilities.Results.ConflictResult;
using BusinessForbiddenResult = Core.Utilities.Results.ForbiddenResult;

namespace Tests.WebAPI;

public sealed class ApiResultMapperTests
{
    private sealed class TestController : ControllerBase { }

    [Theory]
    [InlineData(typeof(BusinessNotFoundResult), 404)]
    [InlineData(typeof(BusinessForbiddenResult), 403)]
    [InlineData(typeof(BusinessConflictResult), 409)]
    [InlineData(typeof(ErrorResult), 400)]
    public void MapsBusinessResultSemanticsToHttpStatus(Type resultType, int expectedStatus)
    {
        IResult result = resultType == typeof(BusinessNotFoundResult)
            ? new BusinessNotFoundResult("missing")
            : resultType == typeof(BusinessForbiddenResult)
                ? new BusinessForbiddenResult("forbidden")
                : resultType == typeof(BusinessConflictResult)
                    ? new BusinessConflictResult("conflict")
                    : new ErrorResult("invalid");

        var action = new TestController().ToHttpResult(result);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(action).StatusCode);
    }

    [Fact]
    public void MapsTypedNotFoundDataResultToNotFound()
    {
        var action = new TestController().ToHttpResult(new NotFoundDataResult<string>("missing"));

        Assert.Equal(404, Assert.IsAssignableFrom<ObjectResult>(action).StatusCode);
    }
}
