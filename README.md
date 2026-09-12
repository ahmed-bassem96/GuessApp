# Guess the Number — ASP.NET Core backend

A small interview assignment using .NET 8, normal API controllers, EF Core, and PostgreSQL. The React frontend is separate. There is no game history or leaderboard.

## Structure

- `Controllers/AuthController.cs`: registration, login, logout, current user, and CSRF token.
- `Controllers/GameController.cs`: start a round, read its state, submit a guess.
- `Data/AppDbContext.cs`: EF mapping and the unique email index.
- `Models/User.cs`: the single database entity.
- `DTOs/`: validated input and explicit responses. Entities are never returned to clients.
- `Migrations/`: the initial EF database migration.
- `Program.cs`: dependency registration, cookies, CORS, CSRF, and error handling. Application endpoints are all controller actions.

Controllers use the EF context directly. This application does not need a repository, service layer, or authentication framework beyond ASP.NET Core's cookie handler and password hasher. `AddControllersWithViews()` supplies MVC's built-in CSRF filters; endpoints still inherit from `ControllerBase`, and no views or frontend are included.

## Data and game rules

Each user has `Id`, normalized `Email`, `PasswordHash`, nullable `BestGuesses`, nullable `SecretNumber`, and `GuessCount`. PostgreSQL's built-in `xmin` is mapped as `Version` for optimistic concurrency; it is not a game statistic.

`BestGuesses` is the **only personal-best field**. Null means the user has not won yet. The current secret/count are transient game state stored on the same row, not historical records.

Starting a round selects a random number using `GetInt32(1, 44)` (upper bound exclusive) and resets the count to zero. Starting another round abandons the unfinished one. Invalid guesses are rejected before the controller runs. Every accepted integer from 1 through 43 counts, including repeated guesses. Neither the secret nor the count comes from the client.

A correct guess updates the best only if it was null or the new count is smaller, then clears the secret to mark the game finished. Guessing without an active game returns 409. The API never returns the secret, even after completion. Login and the current-user endpoint read the best from PostgreSQL.

EF saves the count, secret, and best together. If two requests update the same user simultaneously, the `xmin` check rejects the stale update with 409. Refresh the current game before submitting another request; a rejected update does not count.

## Endpoints

All paths start with `/api`. Send JSON request bodies.

| Method | Path | Body | Success |
| --- | --- | --- | --- |
| GET | `/auth/csrf` | None | 200 `{ "token": "..." }` |
| POST | `/auth/register` | `{ "email": "you@example.com", "password": "at-least-8-characters" }` | 201 user + login cookie |
| POST | `/auth/login` | `{ "email": "you@example.com", "password": "..." }` | 200 user + login cookie |
| POST | `/auth/logout` | None | 204; requires authentication |
| GET | `/auth/me` | None | 200 user; requires authentication |
| POST | `/games` | None | 201 new current game; requires authentication |
| GET | `/games/current` | None | 200 current game; requires authentication |
| POST | `/games/current/guesses` | `{ "number": 22 }` | 200 hint/result; requires authentication |

User response: `{ "id": "...", "email": "you@example.com", "bestGuesses": null }`.

Current game: `{ "isActive": true, "guessCount": 0 }`.

Guess result: `{ "message": "guess higher", "guessCount": 1, "bestGuesses": null }`. Message is exactly `guess higher`, `guess lower`, or `correct`.

Errors: 400 for invalid input or missing/invalid CSRF token; 401 for incorrect credentials or missing authentication; 409 for duplicate email, no active game, or a concurrent update; 500 for unexpected server failures. Validation errors use ASP.NET Core validation responses; expected business errors have a `message`. Unexpected exceptions use the standard exception handler and Problem Details.

## Authentication and React

Passwords use ASP.NET Core `PasswordHasher<User>` (salted PBKDF2), never plaintext. A successful registration also logs the user in. The cookie contains a protected authentication ticket with the user ID, expires after eight hours, and is HttpOnly. React does not read or store the cookie itself.

Logout calls `SignOutAsync`, which expires the browser's auth cookie. Subsequent browser requests are unauthenticated; users and scores stay in PostgreSQL. This simple cookie setup does not maintain server-side session revocation: a previously copied cookie remains usable until expiry. Global logout/revocation would require additional session tracking, which is outside this assignment.

Use `credentials: 'include'` on **every** React API request. Fetch a CSRF token before the first write, and fetch a fresh token after registration, login, or logout because the token is tied to the current identity. Keep the token in memory and send it in `X-CSRF-TOKEN` on POST requests. Cookies alone do not protect against CSRF.

```javascript
const baseUrl = 'http://localhost:5139/api';
let csrfToken;

async function refreshCsrf() {
  const response = await fetch(`${baseUrl}/auth/csrf`, { credentials: 'include' });
  if (!response.ok) throw new Error('Could not get CSRF token');
  csrfToken = (await response.json()).token;
}

async function post(path, body) {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) throw new Error(`Request failed: ${response.status}`);
  return response.status === 204 ? null : response.json();
}

await refreshCsrf();
const user = await post('/auth/login', { email, password });
await refreshCsrf();
await post('/games');
const result = await post('/games/current/guesses', { number: 22 });
```

CORS permits only `FrontendOrigin`, default `http://localhost:5173`, and allows credentials. Run both local servers with HTTP and use `localhost` consistently (do not mix it with `127.0.0.1`). Different ports are different origins but the same site, so `SameSite=Lax` works. In production use HTTPS and the same site for frontend and API, for example `app.example.com` and `api.example.com`. Truly different sites would need changes to both auth and antiforgery cookie SameSite policies and may encounter third-party-cookie restrictions; this configuration intentionally targets same-site hosting.

## Database setup and running

Prerequisites: .NET 8 SDK or a newer SDK supporting net8.0, and a running PostgreSQL server. The PostgreSQL role must own the application database or have permission to create its tables.

Create a **new** database named `guess43` using pgAdmin or `createdb -U postgres guess43`. The earlier raw-SQL prototype used a different schema; this initial migration is for a fresh database and does not migrate prototype data. Do not point it at an unrelated database.

From this project directory, in PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=guess43;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
dotnet restore
dotnet tool restore
dotnet ef database update
dotnet run --launch-profile http
```

The API listens on `http://localhost:5139`. Open `http://localhost:5139/swagger` to use Swagger UI in Development. `WebApplication1.http` also contains example requests. Keep credentials in environment variables, not committed settings. To change the allowed React origin, set `FrontendOrigin` in settings or `$env:FrontendOrigin` before running.

The app does not create tables at startup. Apply the checked-in migration explicitly with `dotnet ef database update`. Future schema changes use `dotnet ef migrations add Name` followed by `dotnet ef database update`.

## Try the API with Swagger

After setting up the database and starting the API, open `http://localhost:5139/swagger`:

1. Expand `POST /api/auth/register`, click **Try it out**, enter an email and a password of at least eight characters, and click **Execute**. Alternatively, use `POST /api/auth/login` for an existing account.
2. Execute `POST /api/games` to start a round.
3. Execute `POST /api/games/current/guesses` with `{ "number": 22 }`, then follow the higher/lower hints until correct.
4. Execute `GET /api/auth/me` to see the personal best.
5. Execute `POST /api/auth/logout`, then log in again to see the saved best.

The browser stores the login cookie automatically. To set a shared CSRF header, execute `GET /api/auth/csrf`, copy the returned `token`, click **Authorize**, paste it under **CsrfToken**, and confirm. Swagger sends it on all endpoints. Obtain and enter a new token after registration, login, or logout because the identity changes. The CSRF token does not sign you in; authentication still uses the browser cookie.

If you leave Authorize empty (or clear it using the dialog's Logout button), Swagger automatically obtains a fresh CSRF token before each write. The dialog's Logout button only clears the saved header; use `POST /api/auth/logout` to sign out of the application. CSRF protection remains enabled. Swagger is available only in Development.

## Build

Run `dotnet build`. The automated test suite is omitted as requested; the final implementation has not been verified end to end.

## Scope and interview discussion

Removed the React implementation, profile editing/account deletion, display name, raw SQL store, manual transactions, rate limiting, custom security-header middleware, static-file hosting, and Minimal API/fallback endpoints. Retained framework validation, cookie authentication, authorization, standard CSRF protection, CORS for React, and basic exception handling.

**CRUD ambiguity:** the brief calls this a CRUD application but specifies authentication, a game, and a personal best. It does not ask for general account update/delete endpoints. Confirm that wording with the interviewer rather than inventing unrelated CRUD features.

For a two-day deadline: finish and understand this backend on day one, then connect React, test the complete user journey, and rehearse the database/authentication/game decisions on day two.
