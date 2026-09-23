using Business.ValidationRules.FluentValidation;
using Entities.DTOs;

namespace Tests.Business.Validation;

public sealed class GovernanceRequestValidatorTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void RejectsInvalidModerationQueuePagination(int page, int pageSize)
    {
        var validator = new PageRequestValidator();

        var result = validator.Validate(new PageRequest { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RequiresReportReasonAndDescription()
    {
        var validator = new ProductReportRequestValidator();

        var result = validator.Validate(new ProductReportRequest { ReasonId = 0, Description = " " });

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void AcceptsValidQueueAndReportRequests()
    {
        Assert.True(new PageRequestValidator().Validate(new PageRequest { Page = 2, PageSize = 25 }).IsValid);
        Assert.True(new ProductReportRequestValidator().Validate(
            new ProductReportRequest { ReasonId = 1, Description = "Incorrect website address" }).IsValid);
    }
}
