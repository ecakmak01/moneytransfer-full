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
    // Log Context (Correlation + Trace ID)
    // ---------------------------------------------------
    private (string CorrelationId, string TraceId) GetLogContext()
    {
        var correlationId = HttpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        var traceId = HttpContext.TraceIdentifier;
        return (correlationId ?? "-", traceId ?? "-");
    }

    // ---------------------------------------------------
    // Downstream Client
    // ---------------------------------------------------
    private HttpClient CreateDownstreamClient()
    {
        var ctx = GetLogContext();

        var client = _clientFactory.CreateClient();

        // Correlation-ID set
        client.DefaultRequestHeaders.Add("X-Correlation-ID", ctx.CorrelationId);

        // Forward Authorization
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
        var ctx = GetLogContext();

        _logger.LogInformation("BFF_TRANSFER_LIST_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            Route = "GET /api/gateway/transfers"
        });

        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        var resp = await client.GetAsync($"{baseUrl}/api/transfers");
        var body = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("BFF_TRANSFER_LIST_RESPONSE {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            StatusCode = (int)resp.StatusCode,
            RawBody = body
        });

        return StatusCode((int)resp.StatusCode, body);
    }


    // ---------------------------------------------------
    // GET TRANSFER BY ID
    // ---------------------------------------------------
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetTransfer(int id)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("BFF_TRANSFER_GET_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            TransferId = id
        });

        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        var resp = await client.GetAsync($"{baseUrl}/api/transfers/{id}");
        var body = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("BFF_TRANSFER_GET_RESPONSE {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            TransferId = id,
            StatusCode = (int)resp.StatusCode
        });

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
        var ctx = GetLogContext();

        _logger.LogInformation("BFF_START_TRANSFER_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            request.FromAccountId,
            request.ToAccountId,
            request.Amount
        });

        var baseUrl = _config["Services:Transfer"] ?? "http://moneytransfer-service:8080";
        var client = CreateDownstreamClient();

        // Idempotency-Key oluþtur
        var idemKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idemKey);

        var resp = await client.PostAsJsonAsync($"{baseUrl}/api/transfers", request);
        var body = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("BFF_START_TRANSFER_RESPONSE {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            IdempotencyKey = idemKey,
            StatusCode = (int)resp.StatusCode
        });

        return StatusCode((int)resp.StatusCode, body);
    }
}
