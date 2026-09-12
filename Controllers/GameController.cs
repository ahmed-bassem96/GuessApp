using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.DTOs;

namespace WebApplication1.Controllers;

[ApiController, Authorize, Route("api/game")]
public sealed class GameController(AppStore store) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await store.GetGame(UserId));

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        await store.StartGame(UserId);
        return Ok(new { attempts = 0, active = true });
    }

    [HttpPost("guess")]
    public async Task<IActionResult> Guess(GuessRequest request)
    {
        var result = await store.Guess(UserId, request.Number);
        return result is null ? Conflict(new { message = "Start a new game first." }) : Ok(result);
    }
}
