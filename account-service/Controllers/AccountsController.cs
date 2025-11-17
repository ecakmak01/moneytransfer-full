using AccountService.Data;
using AccountService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(AppDbContext db, ILogger<AccountsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private (string CorrelationId, string TraceId) GetLogContext()
    {
        var correlationId = HttpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        var traceId = HttpContext.TraceIdentifier;

        return (correlationId ?? "-", traceId ?? "-");
    }

    // -----------------------------------------------------
    // GET /api/accounts
    // -----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ctx = GetLogContext();

        _logger.LogInformation("ACCOUNT_LIST_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            Path = HttpContext.Request.Path
        });

        var accounts = await _db.Accounts.AsNoTracking().ToListAsync();

        return Ok(accounts);
    }

    // -----------------------------------------------------
    // GET /api/accounts/{id}
    // -----------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("ACCOUNT_GET_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            AccountId = id
        });

        var acc = await _db.Accounts.FindAsync(id);
        if (acc == null)
            return NotFound(new { message = "Account not found" });

        return Ok(acc);
    }

    // -----------------------------------------------------
    // POST /api/accounts  (Yeni Hesap)
    // -----------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Account account)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("ACCOUNT_CREATE_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            Owner = account.Owner
        });

        if (string.IsNullOrWhiteSpace(account.Owner))
            return BadRequest(new { message = "Invalid account" });

        account.Balance = 0;

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    // -----------------------------------------------------
    // PUT /api/accounts/{id}/balance
    // -----------------------------------------------------
    public class UpdateBalanceDto
    {
        public decimal Delta { get; set; } // +100 veya -50
    }

    [HttpPut("{id:int}/balance")]
    public async Task<IActionResult> UpdateBalance(int id, [FromBody] UpdateBalanceDto dto)
    {
        var ctx = GetLogContext();

        _logger.LogInformation("ACCOUNT_BALANCE_UPDATE_REQUEST {@log}", new
        {
            ctx.CorrelationId,
            ctx.TraceId,
            AccountId = id,
            Delta = dto.Delta
        });

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest(new { message = "Idempotency-Key header required" });

        var existing = await _db.IdempotencyKeys.FirstOrDefaultAsync(x => x.Key == idempotencyKey);
        if (existing != null)
        {
            _logger.LogWarning("DUPLICATE_IDEMPOTENCY {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId,
                IdempotencyKey = idempotencyKey
            });
            return StatusCode(409, new { message = "Duplicate request" });
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var acc = await _db.Accounts.FindAsync(id);
            if (acc == null)
                return NotFound(new { message = "Account not found" });

            var newBalance = acc.Balance + dto.Delta;
            if (newBalance < 0)
                return BadRequest(new { message = "Insufficient balance" });

            acc.Balance = newBalance;

            _db.IdempotencyKeys.Add(new IdempotencyKey
            {
                Key = idempotencyKey,
                RequestHash = $"{id}:{dto.Delta}"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("ACCOUNT_BALANCE_UPDATED {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId,
                AccountId = id,
                NewBalance = acc.Balance
            });

            return Ok(new
            {
                message = "Balance updated",
                account = acc
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "BALANCE_UPDATE_ERROR {@log}", new
            {
                ctx.CorrelationId,
                ctx.TraceId
            });
            return StatusCode(500, new { message = "Internal error" });
        }
    }
}
