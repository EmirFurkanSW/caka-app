using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<JobStageEntity> JobStages => Set<JobStageEntity>();
    public DbSet<JobParticipantEntity> JobParticipants => Set<JobParticipantEntity>();
    public DbSet<JobStagePlanEntity> JobStagePlans => Set<JobStagePlanEntity>();
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
            e.Property(x => x.HourlyRate).HasPrecision(12, 2);
            e.Property(x => x.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<JobEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<JobStageEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne<JobEntity>()
                .WithMany()
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobParticipantEntity>(e =>
        {
            e.HasKey(x => new { x.JobId, x.UserName });
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.HourlyRate).HasPrecision(12, 2);
            e.Property(x => x.HourlyRateCurrency).HasMaxLength(8);
            e.HasOne<JobEntity>()
                .WithMany()
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobStagePlanEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.PlannedHours).HasPrecision(12, 2);
            e.HasOne<JobStageEntity>()
                .WithMany()
                .HasForeignKey(x => x.JobStageId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.JobStageId, x.UserName }).IsUnique();
        });

        modelBuilder.Entity<WorkLogEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne(x => x.Job)
              .WithMany()
              .HasForeignKey(x => x.JobId)
              .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.JobStage)
                .WithMany()
                .HasForeignKey(x => x.JobStageId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
