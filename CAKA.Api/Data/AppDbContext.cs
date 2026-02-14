using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WorkLogEntity> WorkLogs => Set<WorkLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(x => x.UserName);
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.PasswordHash).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.Department).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<WorkLogEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(2000);
        });
    }
}
