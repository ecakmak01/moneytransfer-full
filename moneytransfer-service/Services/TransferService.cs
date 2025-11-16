using System.Net.Http.Json;
using MoneyTransferService.Data;
using MoneyTransferService.Models;
using Microsoft.EntityFrameworkCore;

namespace MoneyTransferService.Services;

public class TransferService
{
    private readonly MoneyDbContext _db;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<TransferService> _logger;
    private readonly IConfiguration _config;

    public TransferService(
        MoneyDbContext db,
        IHttpClientFactory clientFactory,
        ILogger<TransferService> logger,
        IConfiguration config)
    {
        _db = db;
        _clientFactory = clientFactory;
        _logger = logger;
        _config = config;
    }

    public async Task<Transfer> CreateTransferAsync(int fromId, int toId, decimal amount, string correlationId, string idempotencyKey, string bearerToken)
    {
        // Ensure not duplicate
        var existingKey = await _db.IdempotencyKeys.FirstOrDefaultAsync(x => x.Key == idempotencyKey);
        if (existingKey != null)
        {
            _logger.LogInformation("Duplicate transfer request with idempotencyKey={Key}", idempotencyKey);
            throw new InvalidOperationException("Duplicate transfer request");
        }

        var accountServiceBase = _config.GetValue<string>("Services:Account") ?? "http://account-service:8080";
        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        if (!string.IsNullOrEmpty(bearerToken))
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        // 1. Decrease source account
        var decResp = await client.PostAsJsonAsync($"{accountServiceBase}/api/accounts/{fromId}/balance", -amount);
        if (!decResp.IsSuccessStatusCode)
        {
            var msg = await decResp.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to decrease balance: {Status} - {Body}", decResp.StatusCode, msg);
            throw new InvalidOperationException("Failed to decrease balance");
        }

        // 2. Increase target account
        var incResp = await client.PostAsJsonAsync($"{accountServiceBase}/api/accounts/{toId}/balance", amount);
        if (!incResp.IsSuccessStatusCode)
        {
            var msg = await incResp.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to increase balance: {Status} - {Body}", incResp.StatusCode, msg);
            throw new InvalidOperationException("Failed to increase balance");
        }

        // 3. Save transfer record
        var transfer = new Transfer
        {
            FromAccountId = fromId,
            ToAccountId = toId,
            Amount = amount,
            Status = "Completed"
        };

        _db.Transfers.Add(transfer);
        _db.IdempotencyKeys.Add(new IdempotencyKey
        {
            Key = idempotencyKey,
            RequestHash = $"{fromId}:{toId}:{amount}"
        });

        await _db.SaveChangesAsync();
        return transfer;
    }
}
