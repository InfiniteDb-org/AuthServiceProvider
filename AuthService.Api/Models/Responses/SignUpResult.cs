namespace AuthService.Api.Models.Responses;

public class SignUpResult
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; } 
    public string? UserId { get; set; }
    public string? AccessToken { get; set; }
}
