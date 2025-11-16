using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Bff.Controllers;

[ApiController]
[Route("api/gateway/transfers")]
public class TransfersGatewayController : ControllerBase
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TransfersGatewayController> _logger;

    public TransfersGatewayController(
        IHttpClientFactory clientFactory,
        IConfiguration config,
        ILogger<TransfersGatewayController> logger)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
    }

    // ---------------------------------------------------
    // Downstream Client
    // ---------------------------------------------------
    private HttpClient CreateDownstreamClient()
    {
        var client = _clientFactory.CreateClient();

        // Correlation-ID
        var correlationId =
            Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Authorization token
        if (Request.Headers.TryGetValue("Authorization", out var auth))
            client.DefaultRequestHeaders.Add("Authorization", auth.ToString());

        return client;
    }


    // ---------------------------------------------------
    // GET ALL TRANSFERS
    // ---------------------------------------------------
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListTransfers()
    {
        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        var resp = await client.GetAsync($"{baseUrl}/api/transfers");
        var body = await resp.Content.ReadAsStringAsync();

        return StatusCode((int)resp.StatusCode, body);
    }


    // ---------------------------------------------------
    // GET TRANSFER BY ID
    // ---------------------------------------------------
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetTransfer(int id)
    {
        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        var resp = await client.GetAsync($"{baseUrl}/api/transfers/{id}");
        var body = await resp.Content.ReadAsStringAsync();

        return StatusCode((int)resp.StatusCode, body);
    }


    // ---------------------------------------------------
    // START TRANSFER
    // ---------------------------------------------------
    public record StartTransferRequest(int FromAccountId, int ToAccountId, decimal Amount);

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> StartTransfer([FromBody] StartTransferRequest request)
    {
        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        // Idempotency-Key oluþtur
        var idemKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idemKey);

        var resp = await client.PostAsJsonAsync($"{baseUrl}/api/transfers", request);
        var body = await resp.Content.ReadAsStringAsync();

        return StatusCode((int)resp.StatusCode, body);
    }
}
