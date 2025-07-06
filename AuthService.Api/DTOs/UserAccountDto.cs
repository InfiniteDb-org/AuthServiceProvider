namespace AuthService.Api.DTOs;

public class UserAccountDto
{
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; } = "User";
}