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

    // -----------------------------------------------------
    // GET /api/accounts
    // -----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var accounts = await _db.Accounts.AsNoTracking().ToListAsync();
        return Ok(accounts);
    }

    // -----------------------------------------------------
    // GET /api/accounts/{id}
    // -----------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
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
        if (string.IsNullOrWhiteSpace(account.Owner))
            return BadRequest(new { message = "Invalid account" });

        account.Balance = 0;
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    // -----------------------------------------------------
    // PUT /api/accounts/{id}/balance
    // Idempotency + Delta Ýþlemi (+/-)
    // -----------------------------------------------------
    public class UpdateBalanceDto
    {
        public decimal Delta { get; set; }   // +100 veya -50
    }

    [HttpPut("{id:int}/balance")]
    public async Task<IActionResult> UpdateBalance(int id, [FromBody] UpdateBalanceDto dto)
    {
        var correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest(new { message = "Idempotency-Key header required" });

        // Ayný idempotency gelirse tekrarlama engellenir
        var existing = await _db.IdempotencyKeys.FirstOrDefaultAsync(x => x.Key == idempotencyKey);
        if (existing != null)
        {
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

            // Idempotency kayýt
            _db.IdempotencyKeys.Add(new IdempotencyKey
            {
                Key = idempotencyKey,
                RequestHash = $"{id}:{dto.Delta}"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                message = "Balance updated",
                account = acc
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Balance update error");
            return StatusCode(500, new { message = "Internal error" });
        }
    }
}
