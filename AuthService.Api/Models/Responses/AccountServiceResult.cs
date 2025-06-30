using AuthService.Api.DTOs;

namespace AuthService.Api.Models.Responses;

public class AccountServiceResult
{
    public AccountServiceData? Data { get; set; }
    public string? UserId { get; set; }
    public string? Id { get; set; }
}

public class AccountServiceData
{
    public string? UserId { get; set; }
    public string? Id { get; set; }
    public UserAccountDto? User { get; set; }
}
