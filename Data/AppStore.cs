using System.Security.Cryptography;
using Npgsql;
using WebApplication1.Models;

namespace WebApplication1.Data;

public sealed class AppStore(NpgsqlDataSource source)
{
    public async Task Initialize()
    {
        await using var command = source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS users (
                id uuid PRIMARY KEY,
                email varchar(254) NOT NULL UNIQUE,
                display_name varchar(64) NOT NULL,
                password_hash text NOT NULL,
                best_guesses integer NULL CHECK (best_guesses > 0),
                target integer NULL CHECK (target BETWEEN 1 AND 43),
                attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0)
            );
            """);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<User?> GetUser(Guid id) => await Find("id = $1", id);
    public async Task<User?> FindByEmail(string email) => await Find("email = $1", email);

    private async Task<User?> Find(string condition, object value)
    {
        // condition is an internal constant; user values are bound parameters.
        await using var command = source.CreateCommand($"SELECT id, email, display_name, password_hash, best_guesses FROM users WHERE {condition}");
        command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new User(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt32(4)) : null;
    }

    public async Task Create(User user)
    {
        await using var command = source.CreateCommand("INSERT INTO users(id,email,display_name,password_hash) VALUES ($1,$2,$3,$4)");
        command.Parameters.AddWithValue(user.Id);
        command.Parameters.AddWithValue(user.Email);
        command.Parameters.AddWithValue(user.DisplayName);
        command.Parameters.AddWithValue(user.PasswordHash);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateName(Guid id, string name)
    {
        await using var command = source.CreateCommand("UPDATE users SET display_name=$2 WHERE id=$1");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(name);
        await command.ExecuteNonQueryAsync();
    }

    public async Task Delete(Guid id)
    {
        await using var command = source.CreateCommand("DELETE FROM users WHERE id=$1");
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<GameState> GetGame(Guid id)
    {
        await using var command = source.CreateCommand("SELECT attempts, target IS NOT NULL FROM users WHERE id=$1");
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return new(0, false);
        return new(reader.GetInt32(0), reader.GetBoolean(1));
    }

    public async Task StartGame(Guid id)
    {
        await using var command = source.CreateCommand("UPDATE users SET target=$2, attempts=0 WHERE id=$1");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(RandomNumberGenerator.GetInt32(1, 44));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<GuessResult?> Guess(Guid id, int number)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // Serialize guesses for this account, including requests from multiple tabs.
        await using var read = new NpgsqlCommand("SELECT target, attempts, best_guesses FROM users WHERE id=$1 FOR UPDATE", connection, transaction);
        read.Parameters.AddWithValue(id);
        int target, attempts;
        int? best;
        await using (var reader = await read.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync() || reader.IsDBNull(0)) return null;
            target = reader.GetInt32(0);
            attempts = reader.GetInt32(1) + 1;
            best = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        }
        var direction = number == target ? "correct" : number < target ? "higher" : "lower";
        var newBest = direction == "correct" && (best is null || attempts < best);
        if (newBest) best = attempts;
        await using var update = new NpgsqlCommand("UPDATE users SET attempts=$2, target=$3, best_guesses=$4 WHERE id=$1", connection, transaction);
        update.Parameters.AddWithValue(id);
        update.Parameters.AddWithValue(attempts);
        update.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, direction == "correct" ? DBNull.Value : target);
        update.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, (object?)best ?? DBNull.Value);
        await update.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return new(direction, attempts, best, newBest);
    }
}
