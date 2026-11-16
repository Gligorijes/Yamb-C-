using Yamb.Domain.Enums;
using Yamb.Domain.Events;
using Yamb.Domain.Exceptions;
using Yamb.Domain.Rules;
using Yamb.Domain.Services;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;

namespace Yamb.Domain.Aggregates;

public sealed class YambGame
{
    public Guid Id { get; }
    public GameMode Mode { get; }
    public GameRuleset Rules { get; }
    public IReadOnlyList<Column> Columns { get; }
    public IReadOnlyList<Player> Players { get; }
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => Players[CurrentPlayerIndex];
    public DiceSet CurrentDice { get; private set; }
    public int RollNumber { get; private set; }
    public Row? AnnouncedRow { get; private set; }
    public Row? LastAnnouncementRow { get; private set; }
    public int? LastAnnouncementPlayerIndex { get; private set; }
    public bool AllDiceThrownThisRoll { get; private set; }
    public Row? DirectedRow => GetDirectedRow();
    public bool ObligatoryUnlocked => IsObligatoryUnlocked();
    public bool IsGameOver => Players.All(p => p.Scoresheet.IsComplete);

    private readonly ScoringEngine _engine;
    private readonly Random _rng;
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    public RollPhase Phase => RollNumber switch
    {
        0 => RollPhase.NotStarted,
        1 => RollPhase.AfterFirstRoll,
        2 => RollPhase.AfterSecondRoll,
        _ => RollPhase.AfterThirdRoll
    };

    public YambGame(
        GameMode mode,
        IReadOnlyList<Player> players,
        GameRuleset? rules = null,
        Random? rng = null,
        Guid? id = null,
        IReadOnlyList<Column>? columns = null)
    {
        Mode = mode;
        Rules = rules ?? GameRuleset.Default;
        _engine = new ScoringEngine(Rules);
        _rng = rng ?? Random.Shared;
        Id = id ?? Guid.NewGuid();

        if (players is null || players.Count == 0)
            throw new DomainException("At least one player is required.");
        if (players.Count > 2)
            throw new DomainException("Yamb supports at most 2 players.");
        if (Mode == GameMode.VersusComputer)
            throw new DomainException("VersusComputer mode is not implemented yet.");
        if (Mode == GameMode.SinglePlayer && players.Count != 1)
            throw new DomainException("Single player mode requires exactly one player.");
        if (Mode == GameMode.TwoPlayer && players.Count != 2)
            throw new DomainException("Two player mode requires exactly two players.");

        Players = players;
        Columns = columns is { Count: > 0 }
            ? columns.Distinct().ToList()
            : players[0].Scoresheet.Columns;
        CurrentDice = DiceSet.Initial(Rules.DiceCount);
    }

    public DiceSet Roll()
    {
        if (RollNumber >= Rules.MaxRollsPerTurn)
            throw new DomainException($"Maximum {Rules.MaxRollsPerTurn} rolls per turn. Write a score first.");

        AllDiceThrownThisRoll = CurrentDice.AllDiceThrown || RollNumber == 0;
        CurrentDice = CurrentDice.Reroll(_rng);
        RollNumber++;
        return CurrentDice;
    }

    public void ToggleHold(int dieIndex)
    {
        if (RollNumber == 0)
            throw new DomainException("Roll the dice first.");
        if (RollNumber >= Rules.MaxRollsPerTurn)
            throw new DomainException("Cannot change holds after the final roll.");
        if (dieIndex < 0 || dieIndex >= CurrentDice.Count)
            throw new ArgumentOutOfRangeException(nameof(dieIndex));

        var dice = CurrentDice.Dice.ToList();
        dice[dieIndex] = dice[dieIndex].WithHeld(!dice[dieIndex].IsHeld);
        CurrentDice = new DiceSet(dice);
    }

    public void Announce(Row row)
    {
        if (Phase != RollPhase.AfterFirstRoll)
            throw new DomainException("Announcement can only be made after the first roll.");
        if (AnnouncedRow is not null)
            throw new DomainException("A row has already been announced this turn.");
        if (!Enum.IsDefined(row))
            throw new ArgumentOutOfRangeException(nameof(row));
        if (CurrentPlayer.Scoresheet.IsCellFilled(Column.Announcement, row))
            throw new DomainException($"Row {row} in the Announcement column is already filled.");

        AnnouncedRow = row;
    }

    public void WriteScore(Column column, Row row)
    {
        if (RollNumber == 0)
            throw new DomainException("You must roll at least once before writing a score.");

        if (AnnouncedRow is not null && (column != Column.Announcement || row != AnnouncedRow.Value))
            throw new DomainException(
                $"You announced {AnnouncedRow.Value}; you must write it in the Announcement column this turn.");

        if (!Columns.Contains(column))
            throw new DomainException($"Column {column} is not part of this sheet.");

        var context = new TurnContext(
            Phase,
            AnnouncedRow,
            AllDiceThrownThisRoll,
            GetDirectedRow(),
            IsObligatoryUnlocked());
        var availableRows = ColumnValidator.GetAvailableRows(
            column, CurrentPlayer.Scoresheet.GetColumnCells(column), context);

        if (!availableRows.Contains(row))
            throw new DomainException($"Cannot write to [{column}][{row}] under current rules.");

        int score = _engine.Calculate(CurrentDice.Values, row, RollNumber);

        if (column == Column.Maximum && score != _engine.GetMaximum(row))
            throw new DomainException(
                $"Maximum column requires the highest possible score ({_engine.GetMaximum(row)}) for {row}.");

        int writingPlayerIndex = CurrentPlayerIndex;
        CurrentPlayer.Scoresheet.WriteScore(column, row, score);

        if (column == Column.Announcement)
        {
            LastAnnouncementRow = row;
            LastAnnouncementPlayerIndex = writingPlayerIndex;
        }

        if (IsGameOver)
            _domainEvents.Add(new GameCompletedEvent(GetResult(), DateTime.UtcNow));

        AdvanceTurn();
    }

    public int PreviewScore(Row row)
    {
        if (RollNumber == 0) return 0;
        return _engine.Calculate(CurrentDice.Values, row, RollNumber);
    }

    public int GetMaximumScore(Row row) => _engine.GetMaximum(row);

    public GameResult GetResult()
    {
        if (!IsGameOver) return GameResult.InProgress;

        if (Players.Count == 1) return GameResult.PlayerOneWins;

        int score1 = Players[0].Scoresheet.FinalScore!.Value;
        int score2 = Players[1].Scoresheet.FinalScore!.Value;

        return score1 > score2 ? GameResult.PlayerOneWins
             : score2 > score1 ? GameResult.PlayerTwoWins
             : GameResult.Draw;
    }

    private Row? GetDirectedRow()
    {
        if (!Columns.Contains(Column.Directed)) return null;
        if (LastAnnouncementRow is null) return null;

        if (Mode == GameMode.SinglePlayer) return LastAnnouncementRow;
        return LastAnnouncementPlayerIndex != CurrentPlayerIndex ? LastAnnouncementRow : null;
    }

    private bool IsObligatoryUnlocked()
    {
        if (!Columns.Contains(Column.Obligatory)) return false;

        return Columns
            .Where(column => column is not Column.Obligatory and not Column.Maximum)
            .All(column => CurrentPlayer.Scoresheet
                .GetColumnCells(column)
                .Values
                .All(value => value.HasValue));
    }

    private void AdvanceTurn()
    {
        RollNumber = 0;
        AnnouncedRow = null;
        AllDiceThrownThisRoll = false;
        CurrentDice = new DiceSet(CurrentDice.Dice.Select(d => d.WithHeld(false)).ToList());

        if (!IsGameOver)
            CurrentPlayerIndex = NextActivePlayerIndex();
    }

    private int NextActivePlayerIndex()
    {
        for (int offset = 1; offset <= Players.Count; offset++)
        {
            int candidate = (CurrentPlayerIndex + offset) % Players.Count;
            if (!Players[candidate].Scoresheet.IsComplete)
                return candidate;
        }
        return CurrentPlayerIndex;
    }

    public GameSnapshot ToSnapshot() => new(
        Id,
        Mode,
        CurrentPlayerIndex,
        Players.Select(p => new PlayerSnapshot(
            p.Name,
            p.Scoresheet.GetAllCells()
                .SelectMany(column => column.Value.Select(cell => new CellSnapshot(column.Key, cell.Key, cell.Value)))
                .ToList())).ToList(),
        CurrentDice.Dice.ToList(),
        RollNumber,
        AnnouncedRow,
        AllDiceThrownThisRoll,
        Columns,
        LastAnnouncementRow,
        LastAnnouncementPlayerIndex,
        Rules.DiceCount);

    public static YambGame FromSnapshot(GameSnapshot snapshot, GameRuleset? rules = null, Random? rng = null)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        var ruleset = rules ?? GameRuleset.Default;
        var diceCount = Math.Clamp(snapshot.DiceCount, 5, 6);
        if (diceCount != ruleset.DiceCount)
        {
            ruleset = ruleset with
            {
                DiceCount = diceCount,
                ScoringDiceCount = Math.Min(ruleset.ScoringDiceCount, diceCount)
            };
        }

        var activeColumns = snapshot.Columns is { Count: > 0 }
            ? snapshot.Columns.Distinct().ToList()
            : snapshot.Players
                .SelectMany(player => player.Cells)
                .Select(cell => cell.Column)
                .Distinct()
                .ToList();

        if (activeColumns.Count == 0)
            activeColumns = SheetDefinitions.DefaultColumns.ToList();

        var players = snapshot.Players
            .Select(ps => Player.Restore(ps.Name, Scoresheet.Restore(ruleset, ps.Cells, activeColumns)))
            .ToList();

        return new YambGame(snapshot.Mode, players, ruleset, rng, snapshot.Id, activeColumns)
        {
            CurrentPlayerIndex = Math.Clamp(snapshot.CurrentPlayerIndex, 0, players.Count - 1),
            CurrentDice = new DiceSet(snapshot.Dice),
            RollNumber = snapshot.RollNumber,
            AnnouncedRow = snapshot.AnnouncedRow,
            LastAnnouncementRow = snapshot.LastAnnouncementRow,
            LastAnnouncementPlayerIndex = snapshot.LastAnnouncementPlayerIndex,
            AllDiceThrownThisRoll = snapshot.AllDiceThrownThisRoll
        };
    }
}
