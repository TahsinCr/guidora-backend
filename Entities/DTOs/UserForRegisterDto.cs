using Core.Entities;

namespace Entities.DTOs;

public class UserForRegisterDto : IDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
}
