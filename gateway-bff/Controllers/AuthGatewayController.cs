using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Bff.Controllers;

[ApiController]
[Route("auth")]
public class AuthGatewayController : ControllerBase
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthGatewayController> _logger;

    public AuthGatewayController(
        IHttpClientFactory clientFactory,
        IConfiguration config,
        ILogger<AuthGatewayController> logger)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
    }

    public record LoginRequest(string Username);

    // Log context helper
    private (string CorrelationId, string TraceId) GetLogContext()
    {
        var correlationId = HttpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        var traceId = HttpContext.TraceIdentifier;
        return (correlationId ?? "-", traceId ?? "-");
    }

    // -----------------------------------------------------
    // POST /auth/token
    // ACCOUNT SERVICE TOKEN GATEWAY
    // -----------------------------------------------------
    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("BFF_AUTH_TOKEN_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            request.Username
        });

        var client = _clientFactory.CreateClient();

        // ACCOUNT SERVICE URL
        var baseUrl = _config["Services:Account"] ?? "http://account-service:8080";

        var url = $"{baseUrl}/auth/token";

        var http = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        // Correlation-ID forward
        http.Headers.Add("X-Correlation-ID", ctx.CorrelationId);

        var resp = await client.SendAsync(http);
        var body = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("BFF_AUTH_TOKEN_RESPONSE {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            StatusCode = (int)resp.StatusCode,
            RawBody = body
        });

        return StatusCode((int)resp.StatusCode, body);
    }
}
