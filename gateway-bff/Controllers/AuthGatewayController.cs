using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
    {
        var client = _clientFactory.CreateClient();

        // ACCOUNT SERVICE URL
        var baseUrl = _config["Services:Account"] ?? "http://account-service:8080";

        var resp = await client.PostAsJsonAsync($"{baseUrl}/auth/token", request);

        var body = await resp.Content.ReadAsStringAsync();

        return StatusCode((int)resp.StatusCode, body);
    }
}
