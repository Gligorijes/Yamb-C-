using FluentAssertions;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Events;
using Yamb.Domain.Exceptions;
using Yamb.Domain.Rules;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;

namespace Yamb.Domain.Tests;

public class YambGameTests
{
    private static YambGame SinglePlayerGame(
        IReadOnlyList<int>? dice = null,
        int rollNumber = 1,
        Row? announced = null,
        bool allThrown = true,
        IReadOnlyList<CellSnapshot>? cells = null,
        IReadOnlyList<Column>? columns = null) =>
        BuildGame(GameMode.SinglePlayer, 0, "P1",
            cells is null ? null : new[] { cells },
            dice ?? new[] { 6, 6, 6, 6, 6, 1 },
            rollNumber, announced, allThrown, columns);

    private static YambGame TwoPlayerGame(
        IReadOnlyList<CellSnapshot>? player1Cells,
        IReadOnlyList<CellSnapshot>? player2Cells,
        int currentPlayerIndex,
        IReadOnlyList<int>? dice = null,
        int rollNumber = 1,
        Row? announced = null,
        IReadOnlyList<Column>? columns = null) =>
        BuildGame(GameMode.TwoPlayer, currentPlayerIndex, "P1",
            new[] { player1Cells, player2Cells },
            dice ?? new[] { 6, 6, 6, 6, 6, 1 },
            rollNumber, announced, true, columns);

    private static YambGame BuildGame(
        GameMode mode,
        int currentPlayerIndex,
        string player1Name,
        IReadOnlyList<IReadOnlyList<CellSnapshot>?>? playerCells,
        IReadOnlyList<int> dice,
        int rollNumber,
        Row? announced,
        bool allThrown,
        IReadOnlyList<Column>? columns = null)
    {
        var playerCount = mode == GameMode.TwoPlayer ? 2 : 1;
        var players = new List<PlayerSnapshot>();
        for (int i = 0; i < playerCount; i++)
        {
            var cells = playerCells is not null && i < playerCells.Count ? playerCells[i] : null;
            players.Add(new PlayerSnapshot(i == 0 ? player1Name : $"P{i + 1}", cells ?? new List<CellSnapshot>()));
        }

        var snapshot = new GameSnapshot(
            Guid.NewGuid(), mode, currentPlayerIndex, players,
            dice.Select(v => new Die(v)).ToList(),
            rollNumber, announced, allThrown, columns);

        return YambGame.FromSnapshot(snapshot);
    }

    private static IReadOnlyList<CellSnapshot> CompleteCells(Column? highColumn = null)
    {
        var high = new Dictionary<Row, int>
        {
            [Row.Ones] = 5, [Row.Twos] = 10, [Row.Threes] = 15, [Row.Fours] = 20,
            [Row.Fives] = 25, [Row.Sixes] = 30, [Row.Max] = 30, [Row.Min] = 5,
            [Row.Straight] = 0, [Row.ThreeOfAKind] = 0, [Row.FullHouse] = 0,
            [Row.FourOfAKind] = 0, [Row.Yamb] = 0
        };

        return SheetDefinitions.DefaultColumns
            .SelectMany(column => Enum.GetValues<Row>().Select(row =>
                new CellSnapshot(column, row, highColumn == column ? high[row] : 0)))
            .ToList();
    }

    private static IReadOnlyList<CellSnapshot> CompleteExcept(Column column, Row row) =>
        CompleteCells().Where(c => !(c.Column == column && c.Row == row)).ToList();

    [Fact]
    public void Roll_AllowsAtMostThreeRolls()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });

        game.Roll();
        game.Roll();
        game.Roll();

        var act = () => game.Roll();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Roll_FirstRoll_AlwaysQualifiesForHandColumn()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.AllDiceThrownThisRoll.Should().BeTrue();
    }

    [Fact]
    public void Roll_SecondRollWithHeldDice_DoesNotQualifyForHandColumn()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.ToggleHold(0);
        game.Roll();

        game.AllDiceThrownThisRoll.Should().BeFalse();
    }

    [Fact]
    public void Roll_SecondRollWithoutHolds_QualifiesForHandColumn()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.Roll();

        game.AllDiceThrownThisRoll.Should().BeTrue();
    }

    [Fact]
    public void WriteScore_RequiresAtLeastOneRoll()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        var act = () => game.WriteScore(Column.Downward, Row.Ones);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToggleHold_BeforeRoll_Throws()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        var act = () => game.ToggleHold(0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToggleHold_AfterFinalRoll_Throws()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.Roll();
        game.Roll();
        var act = () => game.ToggleHold(0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToggleHold_InvalidIndex_Throws()
    {
        var game = SinglePlayerGame();
        var act = () => game.ToggleHold(6);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToggleHold_TogglesDieState()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();

        game.ToggleHold(0);
        game.CurrentDice.Dice[0].IsHeld.Should().BeTrue();

        game.ToggleHold(0);
        game.CurrentDice.Dice[0].IsHeld.Should().BeFalse();
    }

    [Fact]
    public void Announce_OnlyValidAfterFirstRoll()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.Roll();

        var act = () => game.Announce(Row.Yamb);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Announce_AfterFirstRoll_SetsAnnouncedRow()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();

        game.Announce(Row.Yamb);

        game.AnnouncedRow.Should().Be(Row.Yamb);
    }

    [Fact]
    public void Announce_TwiceInOneTurn_Throws()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.Roll();
        game.Announce(Row.Yamb);

        var act = () => game.Announce(Row.Ones);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Announce_AlreadyFilledRow_Throws()
    {
        var cells = new[] { new CellSnapshot(Column.Announcement, Row.Yamb, 80) };
        var game = SinglePlayerGame(cells: cells);

        var act = () => game.Announce(Row.Yamb);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Announcement_IsBinding_OtherColumnsBlocked()
    {
        var game = SinglePlayerGame(announced: Row.Yamb);

        var act = () => game.WriteScore(Column.Free, Row.Yamb);
        act.Should().Throw<DomainException>().WithMessage("*announce*");
    }

    [Fact]
    public void Announcement_IsBinding_OtherRowsInAnnouncementColumnBlocked()
    {
        var game = SinglePlayerGame(announced: Row.Yamb);

        var act = () => game.WriteScore(Column.Announcement, Row.Ones);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Announcement_AllowedCell_WritesScore()
    {
        var game = SinglePlayerGame(dice: new[] { 6, 6, 6, 6, 6, 1 }, announced: Row.Yamb);

        game.WriteScore(Column.Announcement, Row.Yamb);

        game.Players[0].Scoresheet.GetCell(Column.Announcement, Row.Yamb).Should().Be(80);
    }

    [Fact]
    public void AnnouncementColumn_WithoutAnnouncement_IsNotWritable()
    {
        var game = SinglePlayerGame();

        var act = () => game.WriteScore(Column.Announcement, Row.Yamb);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void WriteScore_AdvancesTurn_AndResetsState()
    {
        var game = TwoPlayerGame(null, null, currentPlayerIndex: 0);
        game.WriteScore(Column.Downward, Row.Ones);

        game.CurrentPlayerIndex.Should().Be(1);
        game.RollNumber.Should().Be(0);
        game.AnnouncedRow.Should().BeNull();
        game.CurrentDice.AllDiceThrown.Should().BeTrue();
    }

    [Fact]
    public void TwoPlayer_SkipsPlayerWithCompletedSheet()
    {
        var game = TwoPlayerGame(CompleteCells(), null, currentPlayerIndex: 1);

        game.WriteScore(Column.Downward, Row.Ones);

        game.CurrentPlayerIndex.Should().Be(1);
    }

    [Fact]
    public void TwoPlayer_GameOver_WhenBothSheetsComplete()
    {
        var game = TwoPlayerGame(CompleteCells(), CompleteCells(), currentPlayerIndex: 0);
        game.IsGameOver.Should().BeTrue();
    }

    [Fact]
    public void TwoPlayer_HigherScoreWins()
    {
        var game = TwoPlayerGame(CompleteCells(highColumn: Column.Downward), CompleteCells(), currentPlayerIndex: 0);
        game.GetResult().Should().Be(GameResult.PlayerOneWins);
    }

    [Fact]
    public void TwoPlayer_EqualScores_Draw()
    {
        var game = TwoPlayerGame(CompleteCells(), CompleteCells(), currentPlayerIndex: 0);
        game.GetResult().Should().Be(GameResult.Draw);
    }

    [Fact]
    public void SinglePlayer_GameOver_PlayerOneWins()
    {
        var game = SinglePlayerGame(cells: CompleteCells());
        game.IsGameOver.Should().BeTrue();
        game.GetResult().Should().Be(GameResult.PlayerOneWins);
    }

    [Fact]
    public void GetResult_InProgress_WhenNotOver()
    {
        var game = SinglePlayerGame();
        game.GetResult().Should().Be(GameResult.InProgress);
    }

    [Fact]
    public void PreviewScore_BeforeRoll_ReturnsZero()
    {
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("P1") });
        game.PreviewScore(Row.Yamb).Should().Be(0);
    }

    [Fact]
    public void PreviewScore_WithFixedDice_ReturnsEngineValue()
    {
        var game = SinglePlayerGame(dice: new[] { 6, 6, 6, 6, 6, 1 });
        game.PreviewScore(Row.Yamb).Should().Be(80);
    }

    [Fact]
    public void WriteScore_OnGameOver_RaisesGameCompletedEvent()
    {
        var game = SinglePlayerGame(cells: CompleteExcept(Column.Downward, Row.Ones));

        game.WriteScore(Column.Downward, Row.Ones);

        game.IsGameOver.Should().BeTrue();
        game.DomainEvents.OfType<GameCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Snapshot_Roundtrip_PreservesState()
    {
        var game = new YambGame(GameMode.TwoPlayer,
            new[] { new Player("P1"), new Player("P2") });
        game.Roll();
        game.ToggleHold(2);
        game.Announce(Row.FullHouse);

        var restored = YambGame.FromSnapshot(game.ToSnapshot());

        restored.Id.Should().Be(game.Id);
        restored.Mode.Should().Be(game.Mode);
        restored.CurrentPlayerIndex.Should().Be(game.CurrentPlayerIndex);
        restored.RollNumber.Should().Be(game.RollNumber);
        restored.AnnouncedRow.Should().Be(Row.FullHouse);
        restored.CurrentDice.Dice.Should().BeEquivalentTo(game.CurrentDice.Dice);
        restored.Players.Select(p => p.Name).Should().BeEquivalentTo("P1", "P2");
    }

    [Fact]
    public void TenColumnSheet_UsesAllVariantColumns()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var game = SinglePlayerGame(columns: columns);

        game.Columns.Should().Equal(columns);
        game.Players[0].Scoresheet.TotalCellCount.Should().Be(130);
        game.Players[0].Scoresheet.GetAllCells().Should().ContainKey(Column.Maximum);
    }

    [Fact]
    public void Directed_AfterOpponentAnnouncement_UsesAnnouncedRow()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var game = TwoPlayerGame(null, null, currentPlayerIndex: 0, columns: columns);

        game.Announce(Row.Yamb);
        game.WriteScore(Column.Announcement, Row.Yamb);

        game.CurrentPlayerIndex.Should().Be(1);
        game.DirectedRow.Should().Be(Row.Yamb);

        game.Roll();
        game.WriteScore(Column.Directed, Row.Yamb);
        game.Players[1].Scoresheet.GetCell(Column.Directed, Row.Yamb).Should().HaveValue();

        game.CurrentPlayerIndex.Should().Be(0);
        game.DirectedRow.Should().BeNull();
    }

    [Fact]
    public void Directed_WithoutAnnouncement_CannotBeWritten()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var game = SinglePlayerGame(columns: columns);

        var act = () => game.WriteScore(Column.Directed, Row.Ones);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligatory_LockedUntilOtherColumnsComplete()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var unfinished = SinglePlayerGame(columns: columns);

        unfinished.ObligatoryUnlocked.Should().BeFalse();
        var act = () => unfinished.WriteScore(Column.Obligatory, Row.Ones);
        act.Should().Throw<DomainException>();

        var cells = columns
            .Where(column => column is not Column.Obligatory and not Column.Maximum)
            .SelectMany(column => Enum.GetValues<Row>()
                .Select(row => new CellSnapshot(column, row, 0)))
            .ToList();
        var ready = SinglePlayerGame(cells: cells, columns: columns);

        ready.ObligatoryUnlocked.Should().BeTrue();
        ready.WriteScore(Column.Obligatory, Row.Ones);
        ready.Players[0].Scoresheet.GetCell(Column.Obligatory, Row.Ones).Should().Be(1);
    }

    [Fact]
    public void Maximum_RequiresHighestPossibleScore()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var game = SinglePlayerGame(columns: columns);

        var act = () => game.WriteScore(Column.Maximum, Row.Ones);
        act.Should().Throw<DomainException>().WithMessage("*Maximum column*");

        game.WriteScore(Column.Maximum, Row.Yamb);
        game.Players[0].Scoresheet.GetCell(Column.Maximum, Row.Yamb).Should().Be(80);
    }

    [Fact]
    public void Snapshot_PreservesColumnsAndLastAnnouncement()
    {
        var columns = SheetDefinitions.GetColumns(SheetVariant.Ten);
        var game = SinglePlayerGame(columns: columns);
        game.Announce(Row.FullHouse);
        game.WriteScore(Column.Announcement, Row.FullHouse);

        var restored = YambGame.FromSnapshot(game.ToSnapshot());

        restored.Columns.Should().Equal(columns);
        restored.LastAnnouncementRow.Should().Be(Row.FullHouse);
        restored.LastAnnouncementPlayerIndex.Should().Be(0);
    }

    [Fact]
    public void Constructor_ValidatesPlayerCount()
    {
        var act1 = () => new YambGame(GameMode.SinglePlayer, Array.Empty<Player>());
        act1.Should().Throw<DomainException>();

        var act2 = () => new YambGame(GameMode.TwoPlayer, new[] { new Player("P1") });
        act2.Should().Throw<DomainException>();

        var three = new[] { new Player("P1"), new Player("P2"), new Player("P3") };
        var act3 = () => new YambGame(GameMode.TwoPlayer, three);
        act3.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_ValidatesBotMode()
    {
        var valid = () => new YambGame(GameMode.VersusComputer, new[] { new Player("P1"), new Player("Bot") });
        valid.Should().NotThrow();

        var act = () => new YambGame(GameMode.VersusComputer, new[] { new Player("P1") });
        act.Should().Throw<DomainException>();
    }
}
