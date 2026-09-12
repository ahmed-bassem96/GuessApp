using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;

namespace WebApplication1.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class GameController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Read whether a game is active and its current guess count.</summary>
    /// <remarks>Requires login. The secret number is never included in responses.</remarks>
    [ProducesResponseType(typeof(GameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [HttpGet("current")]
    public async Task<ActionResult<GameResponse>> Current()
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == UserId);
        if (user is null) return Unauthorized();
        return Ok(new GameResponse(user.SecretNumber.HasValue, user.GuessCount));
    }

    /// <summary>Start a new game with a secret number from 1 through 43.</summary>
    /// <remarks>Requires login. Replaces any unfinished round and resets the count to zero. No request body is needed.</remarks>
    [ProducesResponseType(typeof(GameResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<ActionResult<GameResponse>> Start()
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == UserId);
        if (user is null) return Unauthorized();
        user.SecretNumber = RandomNumberGenerator.GetInt32(1, 44);
        user.GuessCount = 0;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The game changed in another request. Refresh and try again." });
        }
        return CreatedAtAction(nameof(Current), new GameResponse(true, 0));
    }

    /// <summary>Submit a guess and receive guess higher, guess lower, or correct.</summary>
    /// <remarks>Requires login and an active game. Send an integer from 1 through 43. Valid guesses count automatically; a winning score updates the personal best only if it improves it. A 409 means no active game or a concurrent update; refresh the current game.</remarks>
    [ProducesResponseType(typeof(GuessResponse), StatusCodes.Status200OK)]
   // [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
   // [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost("current/guesses")]
    public async Task<ActionResult<GuessResponse>> Guess(GuessRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == UserId);
        if (user is null) return Unauthorized();
        if (user.SecretNumber is null)
            return Conflict(new { message = "Start a game before submitting a guess." });

        user.GuessCount++;
        string message;
        if (request.Number < user.SecretNumber)
            message = "guess higher";
        else if (request.Number > user.SecretNumber)
            message = "guess lower";
        else
        {
            message = "correct";
            if (user.BestGuesses is null || user.GuessCount < user.BestGuesses)
                user.BestGuesses = user.GuessCount;
            user.SecretNumber = null;
        }

        // Game state and personal best are saved together in one atomic update.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The game changed in another request. Refresh and try again." });
        }
        return Ok(new GuessResponse(message, user.GuessCount, user.BestGuesses));
    }
}
