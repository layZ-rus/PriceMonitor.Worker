using Microsoft.EntityFrameworkCore;

namespace PriceMonitor.Worker;

public class AppDbContext : DbContext
{
    public DbSet<TrackedItem> TrackedItems { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=price_monitor.db");
    }
}