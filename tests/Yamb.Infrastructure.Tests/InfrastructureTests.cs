using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;
using Yamb.Infrastructure.Persistence;

namespace Yamb.Infrastructure.Tests;

public class EfLeaderboardRepositoryTests
{
    private static YambDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<YambDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new YambDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task AddAndGetTop_OrdersByScoreDescending()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "A", 100, DateTime.UtcNow));
        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "B", 300, DateTime.UtcNow));
        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "C", 200, DateTime.UtcNow));

        var top = await repo.GetTopAsync(2);

        top.Select(e => e.Score).Should().ContainInOrder(300, 200);
    }

    [Fact]
    public async Task Qualifies_EmptyLeaderboard_ReturnsTrue()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        (await repo.QualifiesAsync(1, topN: 10)).Should().BeTrue();
    }

    [Fact]
    public async Task Qualifies_BelowCutoff_ReturnsFalse()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        for (int i = 0; i < 10; i++)
        {
            await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), $"P{i}", 100 + i, DateTime.UtcNow));
        }

        (await repo.QualifiesAsync(95, topN: 10)).Should().BeFalse();
        (await repo.QualifiesAsync(999, topN: 10)).Should().BeTrue();
    }

    [Fact]
    public async Task TrimTo_RemovesEntriesBeyondMax()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        for (int i = 0; i < 5; i++)
        {
            await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), $"P{i}", i * 10, DateTime.UtcNow));
        }

        await repo.TrimToAsync(3);

        (await repo.GetTopAsync(10)).Should().HaveCount(3);
    }
}

public class EfGameRepositoryTests
{
    private static YambDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<YambDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new YambDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SaveAndLoad_PreservesGameState()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.TwoPlayer,
            new[] { new Player("Ana"), new Player("Bog") });
        game.Roll();
        game.ToggleHold(1);
        game.Announce(Row.FullHouse);

        await repo.SaveAsync(game);
        var loaded = await repo.GetByIdAsync(game.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(game.Id);
        loaded.Mode.Should().Be(GameMode.TwoPlayer);
        loaded.Players.Select(p => p.Name).Should().BeEquivalentTo("Ana", "Bog");
        loaded.RollNumber.Should().Be(1);
        loaded.AnnouncedRow.Should().Be(Row.FullHouse);
        loaded.CurrentDice.Dice.Should().BeEquivalentTo(game.CurrentDice.Dice);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesFiveDiceRules()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var rules = GameRuleset.Default with { DiceCount = 5, ScoringDiceCount = 5 };
        var game = new YambGame(
            GameMode.SinglePlayer,
            new[] { new Player("Ana", rules) },
            rules);
        game.Roll();

        await repo.SaveAsync(game);
        var loaded = await repo.GetByIdAsync(game.Id);

        loaded.Should().NotBeNull();
        loaded!.Rules.DiceCount.Should().Be(5);
        loaded.Rules.ScoringDiceCount.Should().Be(5);
        loaded.CurrentDice.Dice.Should().HaveCount(5);
    }

    [Fact]
    public async Task SaveTwice_UpdatesExistingSnapshot()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });

        await repo.SaveAsync(game);
        game.Roll();
        game.WriteScore(Column.Downward, Row.Ones);
        await repo.SaveAsync(game);

        var loaded = await repo.GetByIdAsync(game.Id);
        loaded!.RollNumber.Should().Be(0);
        loaded.Players[0].Scoresheet.GetCell(Column.Downward, Row.Ones).Should().HaveValue();
    }

    [Fact]
    public async Task WriteOnLoadedGame_PersistsCorrectly()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        game.Roll();
        await repo.SaveAsync(game);

        var loaded = await repo.GetByIdAsync(game.Id);
        loaded!.WriteScore(Column.Downward, Row.Ones);
        await repo.SaveAsync(loaded);

        var reloaded = await repo.GetByIdAsync(game.Id);
        reloaded!.Players[0].Scoresheet.GetCell(Column.Downward, Row.Ones).Should().HaveValue();
    }

    [Fact]
    public async Task ListSummaries_ReturnsSavedGames()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        await repo.SaveAsync(game);

        var summaries = await repo.ListSummariesAsync();

        summaries.Should().ContainSingle();
        summaries[0].Id.Should().Be(game.Id);
        summaries[0].PlayerNames.Should().Be("Ana");
        summaries[0].Mode.Should().Be(GameMode.SinglePlayer);
    }

    [Fact]
    public async Task Delete_RemovesSavedGame()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        await repo.SaveAsync(game);

        await repo.DeleteAsync(game.Id);

        (await repo.GetByIdAsync(game.Id)).Should().BeNull();
        (await repo.ListSummariesAsync()).Should().BeEmpty();
    }
}
