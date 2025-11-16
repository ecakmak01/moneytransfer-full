using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseRouting();        
app.UseCors("AllowUI");    
app.UseAuthentication();
app.UseAuthorization();

// ------------- TOKEN -------------
app.MapPost("/auth/token", async (HttpContext ctx, IHttpClientFactory f) =>
{
    var client = f.CreateClient();
    var body = await ctx.Request.ReadFromJsonAsync<object>();

    var resp = await client.PostAsJsonAsync("http://account-service:8080/auth/token", body);
    return Results.Text(await resp.Content.ReadAsStringAsync(), "application/json");
});

// ------------- TRANSFER POST -------------
app.MapPost("/transfer", async (HttpContext ctx, IHttpClientFactory f) =>
{
    var client = f.CreateClient();
    var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

    var req = new HttpRequestMessage(HttpMethod.Post, "http://moneytransfer-service:8080/transfer")
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    // Authorization
    if (ctx.Request.Headers.TryGetValue("Authorization", out var token))
        req.Headers.TryAddWithoutValidation("Authorization", token.ToString());

    //  Idempotency-Key header'ýný aynen aktar
    if (ctx.Request.Headers.TryGetValue("Idempotency-Key", out var idem))
        req.Headers.TryAddWithoutValidation("Idempotency-Key", idem.ToString());

    var resp = await client.SendAsync(req);
    var content = await resp.Content.ReadAsStringAsync();

    return Results.Text(content, "application/json");
}).RequireAuthorization();

// ------------- TRANSFER GET -------------
app.MapGet("/transfer", async (HttpContext ctx, IHttpClientFactory f) =>
{
    var client = f.CreateClient();

    var req = new HttpRequestMessage(HttpMethod.Get, "http://moneytransfer-service:8080/transfer");

    if (ctx.Request.Headers.TryGetValue("Authorization", out var token))
        req.Headers.TryAddWithoutValidation("Authorization", token.ToString());

    var resp = await client.SendAsync(req);
    return Results.Text(await resp.Content.ReadAsStringAsync(), "application/json");
}).RequireAuthorization();

app.Run();
