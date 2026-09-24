using Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Utilities;

public static class ApiResultMapper
{
    public static IActionResult ToHttpResult(this ControllerBase controller, IResult result)
    {
        if (result.Success) return controller.Ok(result);
        return result switch
        {
            IConflictResult => controller.Conflict(result),
            INotFoundResult => controller.NotFound(result),
            _ => controller.BadRequest(result)
        };
    }
}
