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

    // Helper → Log context
    private (string CorrelationId, string TraceId) GetLogContext()
    {
        var correlationId = HttpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        var traceId = HttpContext.TraceIdentifier;

        return (correlationId ?? "-", traceId ?? "-");
    }

    // --------------------------------------------------------------------
    // GET: /api/transfers
    // Tüm transfer geçmişi
    // --------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ctx = GetLogContext();

        _logger.LogInformation("TRANSFER_LIST_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            Path = HttpContext.Request.Path
        });

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
        var ctx = GetLogContext();

        _logger.LogInformation("TRANSFER_GET_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            TransferId = id
        });

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
    // Idempotent transfer başlatma
    // --------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartTransferRequest request)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("TRANSFER_START_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            request.FromAccountId,
            request.ToAccountId,
            request.Amount
        });

        // 1) GEÇERSİZ TUTAR KONTROLÜ
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
        {
            _logger.LogWarning("TRANSFER_START_UNAUTHORIZED {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId
            });

            return Unauthorized(new { message = "Bearer token missing." });
        }

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

            _logger.LogInformation("TRANSFER_CREATED {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId,
                TransferId = transfer.Id
            });

            return CreatedAtAction(nameof(GetById), new { id = transfer.Id }, transfer);
        }
        catch (InvalidOperationException ex)
        {
            // Bakiye yetersiz vs.
            _logger.LogWarning("TRANSFER_START_INVALID_OPERATION {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId,
                Error = ex.Message
            });

            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Geçersiz hesap
            _logger.LogWarning("TRANSFER_START_ARGUMENT_ERROR {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId,
                Error = ex.Message
            });

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TRANSFER_START_FATAL_ERROR {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId
            });

            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
