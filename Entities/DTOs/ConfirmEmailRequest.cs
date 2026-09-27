namespace Entities.DTOs;

public sealed class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}
