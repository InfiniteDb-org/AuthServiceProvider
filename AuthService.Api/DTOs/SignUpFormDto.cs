using System.ComponentModel.DataAnnotations;

namespace AuthService.Api.DTOs;

public class SignUpFormDto
{
    [Required]
    public string Email { get; set; } = null!;
    
}
