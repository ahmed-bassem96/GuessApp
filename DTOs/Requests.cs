using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string Password);

public record GuessRequest([Range(1, 43)] int Number);
