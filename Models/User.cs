namespace WebApplication1.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int? BestGuesses { get; set; }
    public int? SecretNumber { get; set; }
    public int GuessCount { get; set; }

    // PostgreSQL's xmin changes on every update; EF uses it to detect overlapping requests.
    public uint Version { get; set; }
}
