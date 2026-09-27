using Core.Utilities.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Utilities;

public static class ApiResultMapper
{
    public static IActionResult ToHttpResult(this ControllerBase controller, Core.Utilities.Results.IResult result)
    {
        if (result.Success) return controller.Ok(result);
        return result switch
        {
            IServiceUnavailableResult => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, result),
            IForbiddenResult => controller.StatusCode(StatusCodes.Status403Forbidden, result),
            IConflictResult => controller.Conflict(result),
            INotFoundResult => controller.NotFound(result),
            _ => controller.BadRequest(result)
        };
    }
}
