using System.Collections.Concurrent;
using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;
using Yamb.Domain.Services;

namespace Yamb.Application.Services;

public sealed class BotAdvisor : IBotAdvisor
{
    private static readonly ConcurrentDictionary<int, IReadOnlyList<RollOutcome>> OutcomeCache = new();
    private static readonly ConcurrentDictionary<(int Count, int Limit), IReadOnlyList<RollOutcome>> SampledOutcomeCache = new();
    private static readonly ConcurrentDictionary<string, IReadOnlyList<int>> ScoreCache = new(StringComparer.Ordinal);
    private readonly GameRuleset _rules;

    public BotAdvisor(GameRuleset? ruleset = null)
    {
        _rules = ruleset ?? GameRuleset.Default;
    }

    public AvailableMoveDto? ChooseMove(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        var rules = RulesFor(game);
        var plan = CreatePlan(game, moves, rules, game.AnnouncedRow);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        return plan.BestMove(counts, game.RollNumber, game.AllDiceThrownThisRoll)?.Move;
    }

    public IReadOnlyList<int> ChooseDiceToHold(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        var rules = RulesFor(game);
        if (game.RollNumber < 1 || game.RollNumber >= rules.MaxRollsPerTurn ||
            game.Dice.Dice.Count == 0)
            return Array.Empty<int>();

        var plan = CreatePlan(game, moves, rules, game.AnnouncedRow);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        var decision = plan.ChooseHold(counts, game.RollNumber, game.AllDiceThrownThisRoll);
        return DiceToToggle(game, decision.KeptCounts);
    }

    public Row? ChooseAnnouncement(
        GameDto game,
        IReadOnlyList<AvailableMoveDto> moves,
        IReadOnlyList<AvailableMoveDto> candidates)
    {
        if (game.AnnouncedRow.HasValue || game.RollNumber == 0 || candidates.Count == 0)
            return null;

        var rules = RulesFor(game);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        var baseline = CreatePlan(game, moves, rules, null)
            .ExpectedValue(counts, game.RollNumber, game.AllDiceThrownThisRoll);

        var shortlist = candidates
            .Where(candidate => !IsFilled(game, candidate.Column, candidate.Row))
            .OrderByDescending(candidate => candidate.PreviewScore)
            .Take(5)
            .ToList();

        AvailableMoveDto? best = null;
        double bestValue = double.NegativeInfinity;
        foreach (var candidate in shortlist)
        {
            double value = CreatePlan(game, new[] { candidate }, rules, candidate.Row)
                .ExpectedValue(counts, game.RollNumber, game.AllDiceThrownThisRoll);
            if (value > bestValue)
            {
                bestValue = value;
                best = candidate;
            }
        }

        return bestValue > baseline + 1.5 ? best?.Row : null;
    }

    private Plan CreatePlan(
        GameDto game,
        IReadOnlyList<AvailableMoveDto> moves,
        GameRuleset rules,
        Row? announcedRow)
    {
        var engine = new ScoringEngine(rules);
        var candidates = BuildCandidates(game, moves, announcedRow);
        return new Plan(this, game, rules, engine, candidates, announcedRow);
    }

    private GameRuleset RulesFor(GameDto game)
    {
        int diceCount = Math.Clamp(game.Dice.Dice.Count, 5, 6);
        return _rules with
        {
            DiceCount = diceCount,
            ScoringDiceCount = Math.Min(_rules.ScoringDiceCount, diceCount)
        };
    }

    private static IReadOnlyList<AvailableMoveDto> BuildCandidates(
        GameDto game,
        IReadOnlyList<AvailableMoveDto> moves,
        Row? announcedRow)
    {
        var candidates = moves.ToList();
        if (announcedRow.HasValue || !game.Columns.Contains(Column.Maximum))
            return candidates;
        if (!game.Players[game.CurrentPlayerIndex].Scoresheet.Cells.TryGetValue(
                Column.Maximum,
                out var cells))
            return candidates;

        foreach (var row in Enum.GetValues<Row>())
        {
            if (cells.TryGetValue(row, out var filled) && filled.HasValue) continue;
            if (candidates.Any(move => move.Column == Column.Maximum && move.Row == row)) continue;
            candidates.Add(new AvailableMoveDto(Column.Maximum, row, 0));
        }

        return candidates;
    }

    private static IReadOnlyList<int> DiceToToggle(GameDto game, int[] keptCounts)
    {
        var dice = game.Dice.Dice;
        var remaining = (int[])keptCounts.Clone();
        var desired = new bool[dice.Count];

        for (int i = 0; i < dice.Count; i++)
        {
            int face = Math.Clamp(dice[i].Value, 1, 6);
            if (remaining[face - 1] <= 0) continue;
            remaining[face - 1]--;
            desired[i] = true;
        }

        return Enumerable.Range(0, dice.Count)
            .Where(index => desired[index] != dice[index].IsHeld)
            .ToList();
    }

    private static bool IsFilled(GameDto game, Column column, Row row) =>
        game.Players[game.CurrentPlayerIndex].Scoresheet.Cells.TryGetValue(column, out var cells) &&
        cells.TryGetValue(row, out var value) && value.HasValue;

    private double MoveValue(
        GameDto game,
        ScoringEngine engine,
        AvailableMoveDto move,
        int score,
        int bonusDelta)
    {
        var sheet = game.Players[game.CurrentPlayerIndex].Scoresheet;
        double fill = sheet.TotalCells == 0 ? 1 : (double)sheet.FilledCells / sheet.TotalCells;
        double open = 1 - fill;
        int maximum = engine.GetMaximum(move.Row);
        bool numberRow = move.Row is >= Row.Ones and <= Row.Sixes;

        double value = score + bonusDelta;
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
            Column.Maximum => 10,
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

    private static int BonusDelta(
        GameDto game,
        ScoringEngine engine,
        AvailableMoveDto move,
        int score)
    {
        if (!game.Players[game.CurrentPlayerIndex].Scoresheet.Cells.TryGetValue(
                move.Column,
                out var cells))
            return 0;
        if (cells.TryGetValue(move.Row, out var existing) && existing.HasValue) return 0;

        int before = ColumnTotal(cells, engine);
        var updated = new Dictionary<Row, int?>(cells) { [move.Row] = score };
        int after = ColumnTotal(updated, engine);
        return after - before - score;
    }

    private static int ColumnTotal(
        IReadOnlyDictionary<Row, int?> cells,
        ScoringEngine engine)
    {
        var numberRows = new[] { Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes };
        int numericSum = numberRows.Sum(row => cells.TryGetValue(row, out var value) ? value ?? 0 : 0);
        int total = engine.CalculateNumbersBonus(cells) ?? numericSum;

        if (engine.CalculateMaxMinBonus(cells) is int maxMin) total += maxMin;

        var combinationRows = new[]
        {
            Row.Straight, Row.ThreeOfAKind, Row.FullHouse, Row.FourOfAKind, Row.Yamb
        };
        foreach (var row in combinationRows)
            total += cells.TryGetValue(row, out var value) ? value ?? 0 : 0;

        return total;
    }

    private static IReadOnlyList<int> RowScores(
        int[] counts,
        int rollNumber,
        ScoringEngine engine,
        GameRuleset rules)
    {
        string key = $"{rules.GetHashCode()}|{rollNumber}|{string.Join('.', counts)}";
        return ScoreCache.GetOrAdd(key, _ =>
        {
            var values = ToValues(counts);
            var scores = new int[14];
            foreach (var row in Enum.GetValues<Row>())
                scores[(int)row] = engine.Calculate(values, row, rollNumber);
            return scores;
        });
    }

    private static int[] ToCounts(IEnumerable<int> values)
    {
        var counts = new int[6];
        foreach (int value in values)
            counts[Math.Clamp(value, 1, 6) - 1]++;
        return counts;
    }

    private static IReadOnlyList<int> ToValues(int[] counts)
    {
        var values = new List<int>();
        for (int face = 1; face <= counts.Length; face++)
            for (int i = 0; i < counts[face - 1]; i++)
                values.Add(face);
        return values;
    }

    private static IEnumerable<int[]> SubCounts(int[] counts)
    {
        IEnumerable<int[]> Enumerate(int face)
        {
            if (face == counts.Length)
            {
                yield return (int[])counts.Clone();
                yield break;
            }

            var current = new int[counts.Length];
            for (int value = 0; value <= counts[face]; value++)
            {
                current[face] = value;
                foreach (var rest in Enumerate(face + 1))
                {
                    rest[face] = value;
                    yield return rest;
                }
            }
        }

        return Enumerate(0);
    }

    private static double HoldPriority(int[] kept)
    {
        int maxCount = kept.Max();
        int maxFace = Array.IndexOf(kept, maxCount) + 1;
        int distinct = kept.Count(value => value > 0);
        double priority = 0;

        for (int face = 1; face <= kept.Length; face++)
            priority += kept[face - 1] * face;

        if (maxCount >= 5) priority += 60 + maxFace;
        else if (maxCount >= 4) priority += 28 + maxFace * 2;
        else if (maxCount >= 3) priority += 12 + maxFace;

        if (distinct >= 5) priority += 22;
        else if (distinct >= 4) priority += 8;

        return priority;
    }

    private static string CountKey(int[] counts) => string.Join('.', counts);

    private static int[] Add(int[] left, int[] right) =>
        Enumerable.Range(0, left.Length).Select(i => left[i] + right[i]).ToArray();

    private static int[] Subtract(int[] left, int[] right) =>
        Enumerable.Range(0, left.Length).Select(i => left[i] - right[i]).ToArray();

    private static int Sum(int[] counts) => counts.Sum();

    private static IReadOnlyList<RollOutcome> Outcomes(int diceCount) =>
        OutcomeCache.GetOrAdd(diceCount, static count =>
        {
            var outcomes = new List<RollOutcome>();
            var counts = new int[6];

            void Enumerate(int face, int remaining)
            {
                if (face == 6)
                {
                    if (remaining != 0) return;
                    int factorial = Factorial(count);
                    for (int i = 0; i < counts.Length; i++)
                        factorial /= Factorial(counts[i]);
                    outcomes.Add(new RollOutcome(
                        (int[])counts.Clone(),
                        factorial / Math.Pow(6, count)));
                    return;
                }

                for (int value = 0; value <= remaining; value++)
                {
                    counts[face] = value;
                    Enumerate(face + 1, remaining - value);
                }

                counts[face] = 0;
            }

            Enumerate(0, count);
            return outcomes;
        });

    private static IReadOnlyList<RollOutcome> Outcomes(int diceCount, int limit)
    {
        var all = Outcomes(diceCount);
        if (all.Count <= limit) return all;

        return SampledOutcomeCache.GetOrAdd((diceCount, limit), _ =>
        {
            var ordered = all
                .OrderByDescending(outcome => outcome.Probability)
                .ThenByDescending(outcome => outcome.Counts.Max())
                .ToList();
            var selected = new List<RollOutcome>(limit);
            for (int i = 0; i < limit; i++)
            {
                int index = (int)((i + 0.5) * ordered.Count / limit);
                selected.Add(ordered[index]);
            }

            double totalWeight = selected.Sum(outcome => outcome.Probability);
            return selected
                .Select(outcome => outcome with { Probability = outcome.Probability / totalWeight })
                .ToList();
        });
    }

    private static int Factorial(int value)
    {
        int result = 1;
        for (int i = 2; i <= value; i++) result *= i;
        return result;
    }

    private sealed record RollOutcome(int[] Counts, double Probability);

    private sealed record MoveChoice(double Value, AvailableMoveDto Move);

    private sealed record HoldDecision(double Value, int[] KeptCounts);

    private sealed class Plan
    {
        private const int RootHoldLimit = 20;
        private const int NestedHoldLimit = 8;
        private const int RootOutcomeLimit = 96;
        private const int NestedOutcomeLimit = 40;

        private readonly BotAdvisor _owner;
        private readonly GameDto _game;
        private readonly GameRuleset _rules;
        private readonly ScoringEngine _engine;
        private readonly IReadOnlyList<AvailableMoveDto> _candidates;
        private readonly Dictionary<(AvailableMoveDto Move, int Score), int> _bonusDeltas = new();
        private readonly Row? _announcedRow;
        private readonly int _startRoll;
        private readonly Dictionary<string, double> _cache = new(StringComparer.Ordinal);

        public Plan(
            BotAdvisor owner,
            GameDto game,
            GameRuleset rules,
            ScoringEngine engine,
            IReadOnlyList<AvailableMoveDto> candidates,
            Row? announcedRow)
        {
            _owner = owner;
            _game = game;
            _rules = rules;
            _engine = engine;
            _candidates = candidates
                .Where(move => !IsFilled(game, move.Column, move.Row))
                .Where(move => !announcedRow.HasValue ||
                    (move.Column == Column.Announcement && move.Row == announcedRow.Value))
                .ToList();
            _announcedRow = announcedRow;
            _startRoll = game.RollNumber;
        }

        public double ExpectedValue(int[] counts, int rollNumber, bool allThrown) =>
            Evaluate(counts, rollNumber, allThrown);

        public MoveChoice? BestMove(int[] counts, int rollNumber, bool allThrown) =>
            BestMoveInternal(counts, rollNumber, allThrown);

        public HoldDecision ChooseHold(int[] counts, int rollNumber, bool allThrown)
        {
            if (rollNumber >= _rules.MaxRollsPerTurn)
                return new HoldDecision(
                    BestMoveInternal(counts, rollNumber, allThrown)?.Value ?? 0,
                    (int[])counts.Clone());

            double best = double.NegativeInfinity;
            int[] bestKept = (int[])counts.Clone();
            foreach (var kept in HoldChoices(counts, rollNumber))
            {
                int nextRoll = rollNumber + 1;
                bool nextAllThrown = Sum(kept) == 0;
                double value = ExpectedRolls(kept, counts, nextRoll, nextAllThrown, OutcomeLimitFor(rollNumber));
                if (value > best + 0.0001 ||
                    (Math.Abs(value - best) <= 0.0001 && Sum(kept) > Sum(bestKept)))
                {
                    best = value;
                    bestKept = (int[])kept.Clone();
                }
            }

            return new HoldDecision(best, bestKept);
        }

        private bool IsRoot(int rollNumber) => rollNumber == _startRoll;

        private int HoldLimitFor(int rollNumber) => IsRoot(rollNumber) ? RootHoldLimit : NestedHoldLimit;

        private int OutcomeLimitFor(int rollNumber) => IsRoot(rollNumber) ? RootOutcomeLimit : NestedOutcomeLimit;

        private IReadOnlyList<int[]> HoldChoices(int[] counts, int rollNumber)
        {
            int total = Sum(counts);
            var choices = SubCounts(counts)
                .Select(kept => (int[])kept.Clone())
                .OrderByDescending(HoldPriority)
                .ThenByDescending(Sum)
                .ThenBy(CountKey, StringComparer.Ordinal)
                .Take(HoldLimitFor(rollNumber))
                .ToList();

            if (!choices.Any(kept => Sum(kept) == total)) choices.Add((int[])counts.Clone());
            if (!choices.Any(kept => Sum(kept) == 0)) choices.Insert(0, new int[counts.Length]);
            return choices;
        }

        private double Evaluate(int[] counts, int rollNumber, bool allThrown)
        {
            string key = $"{rollNumber}{(allThrown ? '1' : '0')}:{string.Join('.', counts)}";
            if (_cache.TryGetValue(key, out double cached)) return cached;

            if (rollNumber >= _rules.MaxRollsPerTurn)
            {
                double terminal = BestMoveInternal(counts, rollNumber, allThrown)?.Value ?? 0;
                _cache[key] = terminal;
                return terminal;
            }

            double best = double.NegativeInfinity;
            foreach (var kept in HoldChoices(counts, rollNumber))
            {
                bool nextAllThrown = Sum(kept) == 0;
                double value = ExpectedRolls(
                    kept,
                    counts,
                    rollNumber + 1,
                    nextAllThrown,
                    OutcomeLimitFor(rollNumber));
                if (value > best) best = value;
            }

            _cache[key] = best;
            return best;
        }

        private double ExpectedRolls(
            int[] kept,
            int[] counts,
            int nextRoll,
            bool nextAllThrown,
            int outcomeLimit)
        {
            var rerolled = Subtract(counts, kept);
            int diceToRoll = Sum(rerolled);
            double expected = 0;
            foreach (var outcome in Outcomes(diceToRoll, outcomeLimit))
            {
                var next = Add(kept, outcome.Counts);
                expected += outcome.Probability * Evaluate(next, nextRoll, nextAllThrown);
            }

            return expected;
        }

        private int BonusDeltaFor(AvailableMoveDto move, int score)
        {
            var key = (move, score);
            if (_bonusDeltas.TryGetValue(key, out int cached)) return cached;

            int delta = BonusDelta(_game, _engine, move, score);
            _bonusDeltas[key] = delta;
            return delta;
        }

        private MoveChoice? BestMoveInternal(int[] counts, int rollNumber, bool allThrown)
        {
            var scores = RowScores(counts, rollNumber, _engine, _rules);
            MoveChoice? best = null;
            foreach (var move in _candidates)
            {
                if (move.Column == Column.Hand && !allThrown) continue;

                int score = scores[(int)move.Row];
                if (move.Column == Column.Maximum &&
                    score != _engine.GetMaximum(move.Row))
                    continue;

                double value = _owner.MoveValue(
                    _game,
                    _engine,
                    move,
                    score,
                    BonusDeltaFor(move, score));
                if (best is null || value > best.Value)
                    best = new MoveChoice(value, move);
            }

            return best;
        }
    }
}
