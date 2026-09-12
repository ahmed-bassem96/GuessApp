namespace WebApplication1.Models;

public sealed record User(Guid Id, string Email, string DisplayName, string PasswordHash, int? BestGuesses);
public sealed record GameState(int Attempts, bool Active);
public sealed record GuessResult(string Direction, int Attempts, int? BestGuesses, bool NewBest);
