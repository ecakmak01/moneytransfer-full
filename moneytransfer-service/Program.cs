using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MoneyTransferService.Data;
using MoneyTransferService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// 1) SERILOG JSON LOGGING
// ---------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(formatter: new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

// ---------------------------------------------------------
// 2) CONTROLLERS
// ---------------------------------------------------------
builder.Services.AddControllers();

// ---------------------------------------------------------
// 3) DATABASE
// ---------------------------------------------------------
builder.Services.AddDbContext<MoneyDbContext>(opts =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    opts.UseNpgsql(cs);
});

// ---------------------------------------------------------
// 4) SERVICES
// ---------------------------------------------------------
builder.Services.AddHttpClient();
builder.Services.AddScoped<TransferService>();

// ---------------------------------------------------------
// 5) JWT AUTH
// ---------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_12345";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "money-app";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "money-clients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ---------------------------------------------------------
// 6) ENSURE DATABASE
// ---------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoneyDbContext>();
    db.Database.EnsureCreated();
}

// ---------------------------------------------------------
// 7) CORRELATION-ID MIDDLEWARE
// ---------------------------------------------------------
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-ID";

    if (!context.Request.Headers.TryGetValue(headerName, out var correlationId) ||
        string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
        context.Request.Headers[headerName] = correlationId;
    }

    // Response’a ayný ID'yi ekle
    context.Response.Headers[headerName] = correlationId!;

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId!))
    {
        await next();
    }
});

// ---------------------------------------------------------
// 8) REQUEST LOGGING (Serilog otomatik HTTP log)
// ---------------------------------------------------------
app.UseSerilogRequestLogging();

// ---------------------------------------------------------
// 9) AUTH
// ---------------------------------------------------------
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------
// 10) ROUTING
// ---------------------------------------------------------
app.MapControllers();

// ---------------------------------------------------------
app.Run();
