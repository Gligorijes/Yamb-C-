using Microsoft.EntityFrameworkCore;
using Yamb.Domain.Enums;

namespace Yamb.Infrastructure.Persistence;

public class LeaderboardEntryEntity
{
    public Guid Id { get; set; }
    public string Participants { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime AchievedAt { get; set; }
}

public class SavedGameEntity
{
    public Guid Id { get; set; }
    public GameMode Mode { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string PlayerNames { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
}

public class YambDbContext : DbContext
{
    public DbSet<LeaderboardEntryEntity> LeaderboardEntries => Set<LeaderboardEntryEntity>();

    public DbSet<SavedGameEntity> SavedGames => Set<SavedGameEntity>();

    public YambDbContext(DbContextOptions<YambDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeaderboardEntryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Participants).HasColumnName("PlayerName").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Score).IsRequired();
            entity.Property(e => e.AchievedAt).IsRequired();
            entity.HasIndex(e => e.Score).IsDescending();
        });

        modelBuilder.Entity<SavedGameEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Mode).IsRequired();
            entity.Property(e => e.SnapshotJson).IsRequired();
            entity.Property(e => e.PlayerNames).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SavedAt).IsRequired();
        });
    }
}
