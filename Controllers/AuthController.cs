using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AppStore store, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new User(Guid.NewGuid(), request.Email.Trim().ToLowerInvariant(), request.DisplayName.Trim(), "", null);
        user = user with { PasswordHash = hasher.HashPassword(user, request.Password) };
        try { await store.Create(user); }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        { return Conflict(new { message = "An account with this email already exists." }); }
        await SignIn(user);
        return StatusCode(201, PublicUser(user));
    }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await store.FindByEmail(request.Email.Trim().ToLowerInvariant());
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email or password is incorrect." });
        await SignIn(user);
        return Ok(PublicUser(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await store.GetUser(UserId);
        return user is null ? Unauthorized() : Ok(PublicUser(user));
    }

    [HttpPut("me"), Authorize]
    public async Task<IActionResult> Update(ProfileRequest request)
    {
        await store.UpdateName(UserId, request.DisplayName.Trim());
        return Ok(PublicUser((await store.GetUser(UserId))!));
    }

    [HttpDelete("me"), Authorize, EnableRateLimiting("auth")]
    public async Task<IActionResult> Delete(DeleteAccountRequest request)
    {
        var user = await store.GetUser(UserId);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Password is incorrect." });
        await store.Delete(UserId);
        await HttpContext.SignOutAsync();
        return NoContent();
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static object PublicUser(User user) => new { user.Id, user.Email, user.DisplayName, user.BestGuesses };
    private Task SignIn(User user) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme)));
}
