using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;
using Yamb.Domain.Services;

namespace Yamb.Application.Services;

public sealed class BotAdvisor : IBotAdvisor
{
    private const int MaxRolls = 3;

    public AvailableMoveDto? ChooseMove(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        if (moves.Count == 0) return null;

        var rules = CreateRules(game);
        var engine = new ScoringEngine(rules);
        return moves
            .OrderByDescending(move => MoveValue(game, engine, move, move.PreviewScore))
            .ThenByDescending(move => move.PreviewScore)
            .FirstOrDefault();
    }

    public IReadOnlyList<int> ChooseDiceToHold(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        if (game.RollNumber < 1 || game.RollNumber >= MaxRolls || moves.Count == 0)
            return Array.Empty<int>();

        var dice = game.Dice.Dice;
        var held = Enumerable.Range(0, dice.Count).Where(i => dice[i].IsHeld).ToList();
        var unheld = Enumerable.Range(0, dice.Count).Where(i => !dice[i].IsHeld).ToList();
        if (unheld.Count == 0) return Array.Empty<int>();

        var rules = CreateRules(game);
        var engine = new ScoringEngine(rules);
        int nextRoll = Math.Min(game.RollNumber + 1, MaxRolls);
        double bestValue = double.NegativeInfinity;
        var bestHold = new List<int>();

        for (int mask = 0; mask < 1 << unheld.Count; mask++)
        {
            var newlyHeld = unheld
                .Where((_, index) => (mask & (1 << index)) != 0)
                .ToList();
            var kept = held.Concat(newlyHeld).ToList();
            var reroll = Enumerable.Range(0, dice.Count).Except(kept).ToList();
            var candidates = BuildCandidates(game, moves, engine, kept.Count > 0);
            double value = ExpectedValue(game, engine, candidates, dice, kept, reroll, nextRoll);

            if (value > bestValue + 0.0001 ||
                (Math.Abs(value - bestValue) <= 0.0001 && newlyHeld.Count < bestHold.Count))
            {
                bestValue = value;
                bestHold = newlyHeld;
            }
        }

        return bestHold;
    }

    public Row? ChooseAnnouncement(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        if (game.AnnouncedRow.HasValue) return null;

        var rules = CreateRules(game);
        var engine = new ScoringEngine(rules);
        var candidate = moves
            .Where(move => move.Column == Column.Announcement && move.PreviewScore >= 30)
            .Where(move => move.Row is Row.Straight or Row.ThreeOfAKind
                or Row.FullHouse or Row.FourOfAKind or Row.Yamb)
            .OrderByDescending(move => MoveValue(game, engine, move, move.PreviewScore))
            .FirstOrDefault();

        return candidate?.Row;
    }

    private static double ExpectedValue(
        GameDto game,
        ScoringEngine engine,
        IReadOnlyList<AvailableMoveDto> candidates,
        IReadOnlyList<DieDto> dice,
        IReadOnlyList<int> kept,
        IReadOnlyList<int> reroll,
        int rollNumber)
    {
        var values = new int[dice.Count];
        foreach (int index in kept)
            values[index] = dice[index].Value;

        if (reroll.Count == 0)
            return BestMoveValue(game, engine, candidates, values, rollNumber);

        double total = 0;
        int outcomes = 0;

        void Enumerate(int depth)
        {
            if (depth == reroll.Count)
            {
                total += BestMoveValue(game, engine, candidates, values, rollNumber);
                outcomes++;
                return;
            }

            int index = reroll[depth];
            for (int value = 1; value <= 6; value++)
            {
                values[index] = value;
                Enumerate(depth + 1);
            }
        }

        Enumerate(0);
        return outcomes == 0 ? 0 : total / outcomes;
    }

    private static double BestMoveValue(
        GameDto game,
        ScoringEngine engine,
        IReadOnlyList<AvailableMoveDto> candidates,
        int[] values,
        int rollNumber)
    {
        if (candidates.Count == 0) return 0;

        var scoreByRow = new Dictionary<Row, int>();
        foreach (var row in candidates.Select(move => move.Row).Distinct())
            scoreByRow[row] = engine.Calculate(values, row, rollNumber);

        double best = double.NegativeInfinity;
        foreach (var move in candidates)
        {
            int score = scoreByRow[move.Row];
            if (move.Column == Column.Maximum && score != engine.GetMaximum(move.Row))
                continue;

            double value = MoveValue(game, engine, move, score);
            if (value > best) best = value;
        }

        return double.IsNegativeInfinity(best) ? 0 : best;
    }

    private static IReadOnlyList<AvailableMoveDto> BuildCandidates(
        GameDto game,
        IReadOnlyList<AvailableMoveDto> moves,
        ScoringEngine engine,
        bool excludeHand)
    {
        var candidates = moves
            .Where(move => !excludeHand || move.Column != Column.Hand)
            .ToList();

        if (!game.Columns.Contains(Column.Maximum)) return candidates;
        if (!game.Players[game.CurrentPlayerIndex].Scoresheet.Cells.TryGetValue(Column.Maximum, out var cells))
            return candidates;

        foreach (var row in Enum.GetValues<Row>())
        {
            if (cells.TryGetValue(row, out var filled) && filled.HasValue) continue;
            if (candidates.Any(move => move.Column == Column.Maximum && move.Row == row)) continue;
            candidates.Add(new AvailableMoveDto(Column.Maximum, row, engine.GetMaximum(row)));
        }

        return candidates;
    }

    private static double MoveValue(
        GameDto game,
        ScoringEngine engine,
        AvailableMoveDto move,
        int score)
    {
        var sheet = game.Players[game.CurrentPlayerIndex].Scoresheet;
        double fill = sheet.TotalCells == 0 ? 1 : (double)sheet.FilledCells / sheet.TotalCells;
        double open = 1 - fill;
        int maximum = engine.GetMaximum(move.Row);
        bool numberRow = move.Row is >= Row.Ones and <= Row.Sixes;

        double value = score;
        value -= Math.Max(0, maximum - score) * (numberRow ? 0.28 : 0.05) * open;
        if (score == 0) value -= 12 * (0.5 + 0.5 * open);

        if (move.Row == Row.Ones) value += score * 0.25;
        if (move.Row == Row.Max) value += score * 0.1;
        if (move.Row == Row.Min) value += (30 - score) * 0.12 * open;
        if (move.Row is Row.Straight or Row.ThreeOfAKind or Row.FullHouse
            or Row.FourOfAKind or Row.Yamb)
            value += score * 0.05;

        value += move.Column switch
        {
            Column.Maximum => 25,
            Column.Announcement => 8,
            Column.Directed => 6,
            Column.Hand => 4,
            Column.Free => 3,
            Column.Downward or Column.Upward or Column.BothEnds
                or Column.MiddleOut or Column.Obligatory => 2,
            _ => 0
        };

        return value;
    }

    private static GameRuleset CreateRules(GameDto game)
    {
        int diceCount = Math.Clamp(game.Dice.Dice.Count, 5, 6);
        return new GameRuleset
        {
            DiceCount = diceCount,
            ScoringDiceCount = Math.Min(5, diceCount)
        };
    }
}
