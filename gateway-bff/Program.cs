using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ==============================
// CORS
// ==============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

builder.Services.AddHttpClient();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});


var app = builder.Build();

app.UseCors("AllowAll");

// ==============================
// 1) JWT TOKEN ENDPOINT
//    POST /auth/token
// ==============================
app.MapPost("/auth/token", ([FromBody] TokenRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username))
        return Results.BadRequest("username required");

 
    // docker-compose.yml içindeki environment ile AYNI deðerler
    var keyString = builder.Configuration["Jwt:Key"] 
                    ?? "super_secret_key_12345_very_secure_67890";

    var issuer   = builder.Configuration["Jwt:Issuer"]   ?? "money-app";
    var audience = builder.Configuration["Jwt:Audience"] ?? "money-clients";

    var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, req.Username)
    };

    var jwt = new JwtSecurityToken(
        issuer:            issuer,
        audience:          audience,
        claims:            claims,
        notBefore:         DateTime.UtcNow,
        expires:           DateTime.UtcNow.AddHours(1),
        signingCredentials: creds);

    var token = new JwtSecurityTokenHandler().WriteToken(jwt);

    return Results.Ok(new { token });
});

// ==============================
// 2) ACCOUNT-SERVICE
//    POST /accounts/{id}/balance
// ==============================
app.MapPost("/accounts/{id}/balance", async (HttpContext ctx, int id) =>
{
    var client = new HttpClient();

    // JWT forward
    if (ctx.Request.Headers.TryGetValue("Authorization", out var auth))
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());

    var url  = $"http://account-service:8080/api/accounts/{id}/balance";
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

    var resp = await client.PostAsync(
        url,
        new StringContent(body, Encoding.UTF8, "application/json"));

    ctx.Response.StatusCode = (int)resp.StatusCode;
    await resp.Content.CopyToAsync(ctx.Response.Body);
});

// ==============================
// 3) TRANSFER-SERVICE
//    POST /transfer
// ==============================
app.MapPost("/transfer", async ctx =>
{
    var client = new HttpClient();

    // JWT forward
    if (ctx.Request.Headers.TryGetValue("Authorization", out var auth))
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());

    // Idempotency-Key & Correlation-ID forward
    if (ctx.Request.Headers.TryGetValue("Idempotency-Key", out var idem))
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", idem.ToString());

    if (ctx.Request.Headers.TryGetValue("X-Correlation-ID", out var corr))
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", corr.ToString());

    var url  = "http://moneytransfer-service:8080/api/transfers";
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

    var resp = await client.PostAsync(
        url,
        new StringContent(body, Encoding.UTF8, "application/json"));

    ctx.Response.StatusCode = (int)resp.StatusCode;
    await resp.Content.CopyToAsync(ctx.Response.Body);
});

// ==============================
// 4) TRANSFER LÝSTE
//    GET /transfer
// ==============================
app.MapGet("/transfer", async ctx =>
{
    var client = new HttpClient();

    if (ctx.Request.Headers.TryGetValue("Authorization", out var auth))
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());

    var resp = await client.GetAsync("http://moneytransfer-service:8080/api/transfers");

    ctx.Response.StatusCode = (int)resp.StatusCode;
    await resp.Content.CopyToAsync(ctx.Response.Body);
});

app.Run();

// ==============================
// RECORD – En Alta (býrak böyle)
// ==============================
public record TokenRequest(string Username);
