using AuthService.Api.DTOs;

namespace AuthService.Api.Models.Responses;

public class SignInResult
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public UserAccountDto? User { get; set; }
}
