using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Models;

namespace MoneyTransferService.Data;

public class MoneyDbContext : DbContext
{
    public MoneyDbContext(DbContextOptions<MoneyDbContext> options) : base(options)
    {
    }

    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transfer>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();
    }

}
