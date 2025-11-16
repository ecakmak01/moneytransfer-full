namespace MoneyTransferService.Models;

public class IdempotencyKey
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string RequestHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
