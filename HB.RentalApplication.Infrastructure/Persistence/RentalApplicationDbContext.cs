using Microsoft.EntityFrameworkCore;

namespace HB.RentalApplication.Infrastructure.Persistence;

public class RentalApplicationDbContext : DbContext
{
    public DbSet<HB.RentalApplication.Domain.RentalApplication> RentalApplications { get; set; } = null!;
    public DbSet<HB.RentalApplication.Domain.RentalApplicationHistory> RentalApplicationHistories { get; set; } = null!;

    public RentalApplicationDbContext(DbContextOptions<RentalApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RentalApplicationEntityTypeConfiguration());
        // Optionally: configure RentalApplicationHistory if needed
        base.OnModelCreating(modelBuilder);
    }
}
