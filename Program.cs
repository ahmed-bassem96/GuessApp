using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using WebApplication1.Data;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection. See README.md.")));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
// Includes MVC's built-in CSRF filters. Endpoints are API controllers; there are no views.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Guess the Number API",
        Version = "v1",
        Description = "Use Try it out, edit the request body, then Execute. Register or log in first, " +
            "then start a game and submit guesses. Swagger automatically sends the authentication cookie and CSRF token."
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "WebApplication1.xml"));
    options.AddSecurityDefinition("CsrfToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-CSRF-TOKEN",
        Description = "Paste the token from GET /api/auth/csrf. It is sent on all endpoints. " +
            "Refresh it after registration, login, or logout. This is a CSRF token, not a login token; " +
            "authentication uses the browser cookie. Leave empty to let Swagger obtain CSRF tokens automatically."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "CsrfToken" }
        }] = Array.Empty<string>()
    });
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "guess43.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        // APIs return status codes, not redirects to an HTML login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "Guess the Number API v1");
        // Respect Authorize's shared token; obtain one automatically when none is entered.
        options.UseRequestInterceptor("""
            function (request) {
                request.credentials = 'same-origin';
                request.headers = request.headers || {};
                if (request.headers['X-CSRF-TOKEN']) return request;
                if (!['GET', 'HEAD', 'OPTIONS'].includes((request.method || 'GET').toUpperCase())) {
                    return fetch('../api/auth/csrf', { credentials: 'same-origin' })
                        .then(function (response) {
                            if (!response.ok) throw new Error('Could not get the CSRF token. Refresh Swagger and try again.');
                            return response.json();
                        })
                        .then(function (data) {
                            request.headers['X-CSRF-TOKEN'] = data.token;
                            return request;
                        });
                }
                return request;
            }
            """.ReplaceLineEndings(" ")); // Swagger 6 embeds this in a JavaScript JSON string.
    });
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
