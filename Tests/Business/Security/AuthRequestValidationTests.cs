using Business.ValidationRules.FluentValidation;
using Entities.DTOs;

namespace Tests.Business.Security;

public sealed class AuthRequestValidationTests
{
    [Fact]
    public void Login_requires_valid_email_and_bounded_password()
    {
        var validator = new UserForLoginValidator();

        Assert.True(validator.Validate(new UserForLoginDto
        {
            Email = "person@example.com",
            Password = "any existing password"
        }).IsValid);
        Assert.False(validator.Validate(new UserForLoginDto
        {
            Email = "not-an-email",
            Password = new string('x', 129)
        }).IsValid);
    }

    [Fact]
    public void Registration_requires_strong_password_and_bounded_names()
    {
        var validator = new UserForRegisterValidator();
        var validRequest = new UserForRegisterDto
        {
            Email = "person@example.com",
            Password = "correct horse battery staple",
            FirstName = "Ada",
            LastName = "Lovelace"
        };

        Assert.True(validator.Validate(validRequest).IsValid);
        validRequest.Password = "short";
        Assert.False(validator.Validate(validRequest).IsValid);
    }
}
