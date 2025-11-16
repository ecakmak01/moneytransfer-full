namespace AccountService.Models;

public class Account
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string Owner { get; set; } = default!;
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
