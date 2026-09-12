namespace WebApplication1.DTOs;

public record UserResponse(Guid Id, string Email, int? BestGuesses);
public record GameResponse(bool IsActive, int GuessCount);
public record GuessResponse(string Message, int GuessCount, int? BestGuesses);
