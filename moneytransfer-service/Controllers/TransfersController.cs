using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Data;
using MoneyTransferService.Models;
using MoneyTransferService.Services;

namespace MoneyTransferService.Controllers;

[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly MoneyDbContext _db;
    private readonly TransferService _service;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(MoneyDbContext db, TransferService service, ILogger<TransfersController> logger)
    {
        _db = db;
        _service = service;
        _logger = logger;
    }

    // --------------------------------------------------------------------
    // GET: /api/transfers
    // Tüm transfer geçmiþi
    // --------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Transfers
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(list);
    }

    // --------------------------------------------------------------------
    // GET: /api/transfers/{id}
    // Tekil transfer
    // --------------------------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var transfer = await _db.Transfers.FindAsync(id);

        if (transfer == null)
            return NotFound(new { message = "Transfer not found" });

        return Ok(transfer);
    }

    // ======================
    // TRANSFER START
    // ======================

    public record StartTransferRequest(int FromAccountId, int ToAccountId, decimal Amount);

    // --------------------------------------------------------------------
    // POST: /api/transfers
    // Idempotent transfer baþlatma
    // --------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartTransferRequest request)
    {
        // 1) GEÇERSÝZ TUTAR KONTROLÜ
        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than 0." });

        // 2) CORRELATION-ID
        var correlationId =
            Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // 3) IDEMPOTENCY KEY (zorunlu)
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { message = "Idempotency-Key header is required." });

        // 4) JWT (Bearer Token)
        var bearerHeader = Request.Headers.Authorization.ToString();
        var bearerToken = bearerHeader.Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(bearerToken))
            return Unauthorized(new { message = "Bearer token missing." });

        try
        {
            var transfer = await _service.CreateTransferAsync(
                request.FromAccountId,
                request.ToAccountId,
                request.Amount,
                correlationId,
                idempotencyKey,
                bearerToken
            );

            return CreatedAtAction(nameof(GetById), new { id = transfer.Id }, transfer);
        }
        catch (InvalidOperationException ex)
        {
            // Bakiye yetersiz vs.
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Geçersiz hesap
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Start Transfer");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
