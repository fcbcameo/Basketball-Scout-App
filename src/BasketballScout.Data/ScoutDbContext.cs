using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data;

public class ScoutDbContext : DbContext
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<StatEvent> StatEvents => Set<StatEvent>();
    public DbSet<QuarterScore> QuarterScores => Set<QuarterScore>();

    public ScoutDbContext(DbContextOptions<ScoutDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Abbreviation).IsRequired().HasMaxLength(5);
            entity.Property(e => e.Color).IsRequired().HasMaxLength(7);

            entity.HasOne(e => e.Season)
                .WithMany(s => s.Teams)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.Team)
                .WithMany(t => t.Players)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.Season)
                .WithMany(s => s.Games)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.HomeTeam)
                .WithMany()
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AwayTeam)
                .WithMany()
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StatEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameClock).HasMaxLength(10);

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Game)
                .WithMany(g => g.StatEvents)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LinkedEvent)
                .WithMany()
                .HasForeignKey(e => e.LinkedEventId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.GameId, e.Timestamp });
        });

        modelBuilder.Entity<QuarterScore>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Game)
                .WithMany(g => g.QuarterScores)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
