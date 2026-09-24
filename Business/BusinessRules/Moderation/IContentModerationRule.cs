using Core.Utilities.Results;

namespace Business.BusinessRules.Moderation;

public interface IContentModerationRule
{
    IResult EnsureAllowed(params string?[] values);
}
