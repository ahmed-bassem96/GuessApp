using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(64, MinimumLength = 1)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 8)] string Password);
public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string Password);
public sealed record ProfileRequest([Required, StringLength(64, MinimumLength = 1)] string DisplayName);
public sealed record GuessRequest([Range(1, 43)] int Number);
public sealed record DeleteAccountRequest([Required, StringLength(128, MinimumLength = 1)] string Password);
