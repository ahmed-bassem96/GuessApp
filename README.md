# Guess the Number API

An ASP.NET Core API for registering an account, signing in, and guessing a secret number from **1 to 43**. The API saves your personal best in PostgreSQL.

## Run the API

Install the .NET 8 SDK (or a compatible newer SDK) and start PostgreSQL. Create a fresh database named `guess43` using pgAdmin or:

```powershell
createdb -U postgres guess43
```

From the project directory, run these commands in PowerShell. Replace `YOUR_LOCAL_PASSWORD` with your PostgreSQL password:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=guess43;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
dotnet restore
dotnet tool restore
dotnet ef database update
dotnet run --launch-profile http
```

The migration creates the required tables; the application does not create them at startup.

API base URL: `http://localhost:5139/api`.

Swagger UI: [Open Swagger](http://localhost:5139/swagger), available in Development.

## Test the API step by step

These steps follow the requests in `WebApplication1.http`. Use an HTTP client with its cookie jar enabled so it retains and sends the CSRF and authentication cookies across requests.

In the examples, `{{baseUrl}}` means `http://localhost:5139/api` and `{{csrfToken}}` means the latest token returned by the CSRF endpoint. Define these variables in your client or replace the placeholders manually.

### 1. Get a CSRF token

```http
GET {{baseUrl}}/auth/csrf
```

Copy the `token` from the JSON response and use it as `{{csrfToken}}` in the `X-CSRF-TOKEN` header on every POST request. Keep the response cookies as well; the token alone is insufficient.

### 2. Register an account

```http
POST {{baseUrl}}/auth/register
Content-Type: application/json
X-CSRF-TOKEN: {{csrfToken}}

{
  "email": "player@example.com",
  "password": "Interview-Password-123!"
}
```

A successful registration returns `201 Created` and signs you in automatically. Use a valid email and a password of 8–128 characters.

**Repeat step 1 after registration** and replace `{{csrfToken}}` with the new token before sending another POST request. You can then skip to step 4.

### 3. Log in with an existing account

If you already have an account, use this instead of registration. Get a CSRF token first as shown in step 1.

```http
POST {{baseUrl}}/auth/login
Content-Type: application/json
X-CSRF-TOKEN: {{csrfToken}}

{
  "email": "player@example.com",
  "password": "Interview-Password-123!"
}
```

A successful login returns `200 OK`, your user details, and an authentication cookie.

**Repeat step 1 after login** and replace `{{csrfToken}}` with the new token.

### 4. View your user details and personal best

```http
GET {{baseUrl}}/auth/me
```

The response includes `id`, `email`, and `bestGuesses`. A `null` personal best means you have not won a game yet.

### 5. Start a new round

```http
POST {{baseUrl}}/games
X-CSRF-TOKEN: {{csrfToken}}
```

A successful request returns `201 Created`:

```json
{ "isActive": true, "guessCount": 0 }
```

Starting a new round replaces any unfinished round and resets the guess count.

### 6. Check the current game

```http
GET {{baseUrl}}/games/current
```

The response shows whether a game is active and its guess count. It never includes the secret number.

### 7. Submit a guess

```http
POST {{baseUrl}}/games/current/guesses
Content-Type: application/json
X-CSRF-TOKEN: {{csrfToken}}

{ "number": 22 }
```

Send an integer from **1 to 43**. The response includes `message`, `guessCount`, and `bestGuesses`.

- `guess higher`: try a larger number.
- `guess lower`: try a smaller number.
- `correct`: the round is complete.

Repeat this step until the answer is correct. Each accepted guess counts, including repeated numbers. A win updates your personal best if you used fewer guesses than before. Use step 4 to view your saved best, or step 5 to play again.

### 8. Log out

```http
POST {{baseUrl}}/auth/logout
X-CSRF-TOKEN: {{csrfToken}}
```

A successful logout returns `204 No Content` and clears the authentication cookie. Your account and personal best remain saved.

**Repeat step 1 after logout** before registering or logging in again.

## Using Swagger instead

Open Swagger, expand an endpoint, select **Try it out**, enter any required JSON body, and select **Execute**. Follow the same registration/login, game, and logout sequence above.

Leave **Authorize** empty to let this project's Swagger configuration fetch a CSRF token automatically before each POST request. The browser handles cookies automatically.

If you enter a token manually under **Authorize → CsrfToken**, get it from `GET /api/auth/csrf` and replace it after registration, login, or logout. The Authorize dialog's **Logout** button only clears that token; use `POST /api/auth/logout` to sign out of the application.

## Common responses

| Status | Meaning |
| --- | --- |
| `400 Bad Request` | Invalid input or a missing/invalid CSRF token. |
| `401 Unauthorized` | Incorrect credentials or you are not signed in. |
| `409 Conflict` | Email already registered, no active game, or a game changed in another request. Read the response message; refresh the current game before retrying a concurrent update. |
