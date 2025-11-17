

using AccountService.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdempotencyKey>().ToTable("IdempotencyKeys_Account");

            base.OnModelCreating(modelBuilder);
        }
    }
}
