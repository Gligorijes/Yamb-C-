using System.Collections.Concurrent;
using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
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
    private readonly IBotExperienceStore _experience;

    public BotAdvisor(GameRuleset? ruleset = null, IBotExperienceStore? experience = null)
    {
        _rules = ruleset ?? GameRuleset.Default;
        _experience = experience ?? NullBotExperienceStore.Instance;
    }

    public AvailableMoveDto? ChooseMove(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        var rules = RulesFor(game);
        var plan = CreatePlan(game, moves, rules, game.AnnouncedRow);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        var choice = plan.BestMove(counts, game.RollNumber, game.AllDiceThrownThisRoll);
        if (choice?.Move is { } chosen)
            Remember(game, BotDecisionKind.Move, BotExperienceKeys.Move(chosen));
        return choice?.Move;
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
        Remember(game, BotDecisionKind.Hold, BotExperienceKeys.Hold(decision.KeptCounts));
        return DiceToToggle(game, decision.KeptCounts);
    }

    public bool ShouldFinishTurn(GameDto game, IReadOnlyList<AvailableMoveDto> moves)
    {
        var rules = RulesFor(game);
        if (game.RollNumber < 1 || game.RollNumber >= rules.MaxRollsPerTurn)
            return false;

        var plan = CreatePlan(game, moves, rules, game.AnnouncedRow);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        var choice = plan.BestMove(counts, game.RollNumber, game.AllDiceThrownThisRoll);
        return choice is not null &&
               plan.IsPremiumWrite(choice.Move, counts, game.RollNumber, game.AllDiceThrownThisRoll);
    }

    public Row? ChooseAnnouncement(
        GameDto game,
        IReadOnlyList<AvailableMoveDto> moves,
        IReadOnlyList<AvailableMoveDto> candidates)
    {
        if (game.AnnouncedRow.HasValue || game.RollNumber == 0 || candidates.Count == 0)
            return null;

        var rules = RulesFor(game);
        var engine = new ScoringEngine(rules);
        var counts = ToCounts(game.Dice.Dice.Select(die => die.Value));
        var baseline = CreatePlan(game, moves, rules, null)
            .ExpectedValue(counts, game.RollNumber, game.AllDiceThrownThisRoll);

        var shortlist = candidates
            .Where(candidate => !IsFilled(game, candidate.Column, candidate.Row))
            .OrderByDescending(candidate => candidate.PreviewScore + engine.GetMaximum(candidate.Row) * 0.25)
            .Take(7)
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

        var selection = bestValue > baseline + 1.5 ? best?.Row : null;
        Remember(game, BotDecisionKind.Announcement, BotExperienceKeys.Announcement(selection));
        return selection;
    }

    private void Remember(GameDto game, BotDecisionKind kind, string moveKey)
    {
        _experience.RememberDecision(new BotDecisionRecord(
            game.Id,
            kind,
            BotExperienceKeys.State(game),
            moveKey,
            DateTime.UtcNow));
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
        bool numberRow = IsNumberRow(move.Row);
        bool free = move.Column == Column.Free;
        bool constrained = IsConstrainedColumn(move.Column);
        IReadOnlyDictionary<Row, int?>? cells = null;
        int columnFilled = 0;
        if (sheet.Cells.TryGetValue(move.Column, out var columnCells))
        {
            cells = columnCells;
            columnFilled = columnCells.Count(pair => pair.Value.HasValue);
        }

        int columnEmpty = Math.Max(0, Enum.GetValues<Row>().Length - columnFilled);
        int remainingTurns = Math.Max(1, sheet.TotalCells - sheet.FilledCells);
        double columnUrgency = Math.Clamp((double)columnEmpty / remainingTurns, 0, 2.5);

        double value = score + bonusDelta;
        double regretWeight = numberRow ? 0.22 : move.Row switch
        {
            Row.Yamb => 0.4,
            Row.FourOfAKind => 0.3,
            Row.FullHouse or Row.Straight or Row.ThreeOfAKind => 0.22,
            _ => 0.06
        };
        value -= Math.Max(0, maximum - score) * regretWeight * (0.35 + 0.65 * open);
        if (score == 0) value -= (move.Row == Row.Yamb ? 20 : 12) * (0.5 + 0.5 * open);
        value -= TargetScorePenalty(move.Row, score, open, fill);

        if (numberRow) value += UpperBonusPotential(cells, move.Row, score);
        if (move.Row is Row.Max or Row.Min or Row.Ones)
            value += MaxMinPotential(cells, move.Row, score, open);
        if (IsHighCeilingRow(move.Row)) value += score * 0.06 * (0.5 + 0.5 * open);
        if (move.Row == Row.Straight && score >= 56) value += 16 * (0.4 + 0.6 * open);

        double columnBias = move.Column switch
        {
            Column.Maximum => 3 + (columnFilled == 0 ? 2 : 0),
            Column.Announcement => 7 + (columnFilled == 0 ? 4 : 0),
            Column.Directed => 4,
            Column.Hand => 4,
            _ => 0
        };
        if (constrained)
        {
            columnBias += (columnFilled == 0 ? 9 : columnFilled <= 3 ? 6 :
                columnFilled >= 10 ? 4 + 2 * columnUrgency : 2) + 2 * columnUrgency;
        }
        if (free) columnBias = -11 + 15 * fill;

        if (IsHighCeilingRow(move.Row) && score < maximum * 0.6 &&
            columnFilled < 4 && !free)
            value -= 5 * open;

        return value + columnBias;
    }

    private static bool IsNumberRow(Row row) => row is >= Row.Ones and <= Row.Sixes;

    private static bool IsHighCeilingRow(Row row) =>
        row is Row.Yamb or Row.FourOfAKind or Row.FullHouse or Row.Straight;

    private static bool IsConstrainedColumn(Column column) =>
        column is Column.Downward or Column.Upward or Column.BothEnds or Column.MiddleOut
            or Column.Hand or Column.Directed or Column.Obligatory or Column.Announcement;

    private static double TargetScorePenalty(Row row, int score, double open, double fill)
    {
        double early = Math.Clamp(1 - fill / 0.45, 0, 1);
        return row switch
        {
            Row.Min => Math.Max(0, score - 9) * 2.2 * (0.5 + 0.5 * open),
            Row.Max => Math.Max(0, 26 - score) * 1.6 * (0.5 + 0.5 * open),
            Row.FullHouse => Math.Max(0, 48 - score) * 1.4 * (0.7 + 0.3 * open),
            Row.ThreeOfAKind => Math.Max(0, 32 - score) * 1.6 * (0.25 + 0.75 * early),
            _ => 0
        };
    }

    private static double UpperBonusPotential(
        IReadOnlyDictionary<Row, int?>? cells,
        Row row,
        int score)
    {
        if (cells is null) return 0;

        int sum = 0;
        int remaining = 0;
        foreach (var candidate in Enum.GetValues<Row>())
        {
            if (!IsNumberRow(candidate)) continue;
            int? value = GetCell(cells, candidate);
            if (value.HasValue) sum += value.Value;
            else remaining++;
        }

        if (GetCell(cells, row) is null)
        {
            sum += score;
            remaining = Math.Max(0, remaining - 1);
        }

        double needed = 60.0 / Math.Max(1, remaining + 1);
        double alignment = (score - needed) / 6.0;
        return Math.Clamp(alignment, -3, 5) * (remaining <= 2 ? 1.3 : 0.8);
    }

    private static double MaxMinPotential(
        IReadOnlyDictionary<Row, int?>? cells,
        Row row,
        int score,
        double open)
    {
        if (cells is null) return 0;

        int ones = row == Row.Ones ? score : GetCell(cells, Row.Ones) ?? 4;
        int max = row == Row.Max ? score : GetCell(cells, Row.Max) ?? 26;
        int min = row == Row.Min ? score : GetCell(cells, Row.Min) ?? 9;
        double potential = Math.Max(0, max - min) * Math.Max(0, ones);
        double weight = row switch
        {
            Row.Max => 0.22,
            Row.Min => 0.24,
            Row.Ones => 0.08,
            _ => 0
        };
        return potential * weight * (0.6 + 0.4 * open);
    }

    private static int? GetCell(IReadOnlyDictionary<Row, int?> cells, Row row) =>
        cells.TryGetValue(row, out int? value) && value.HasValue ? value : null;

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

    private double TargetedHoldPriority(
        int[] kept,
        IReadOnlyList<AvailableMoveDto> candidates,
        string stateKey)
    {
        int maxCount = kept.Max();
        int maxFace = Array.IndexOf(kept, maxCount) + 1;
        int distinct = kept.Count(value => value > 0);
        double priority = HoldPriority(kept);
        priority += ExperienceBonus(BotDecisionKind.Hold, stateKey, BotExperienceKeys.Hold(kept));

        if (candidates.Any(move => move.Row == Row.Straight))
        {
            if (distinct >= 5) priority += 32;
            else if (distinct >= 4) priority += 18;
        }

        if (candidates.Any(move => move.Row == Row.Yamb) && maxCount >= 4)
            priority += 20 + maxCount * maxFace;
        if (candidates.Any(move => move.Row == Row.FourOfAKind) && maxCount >= 4)
            priority += 12 + maxFace * 2;
        if (candidates.Any(move => move.Row == Row.ThreeOfAKind) && maxCount >= 3)
            priority += 8 + maxFace;
        if (candidates.Any(move => move.Row == Row.Ones)) priority += kept[0] * 4;
        if (candidates.Any(move => move.Row == Row.Max)) priority += kept[5] * 5;
        if (candidates.Any(move => move.Row == Row.Min))
            priority += kept[0] * 3 + kept[1] * 2;

        return priority;
    }

    private double ExperienceBonus(BotDecisionKind kind, string stateKey, string moveKey)
    {
        var value = _experience.Get(kind, stateKey, moveKey);
        if (value is null || value.Count <= 0) return 0;

        double confidence = value.Count / (value.Count + 12.0);
        return Math.Clamp(4.0 * confidence * value.Mean, -1.5, 1.5);
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
            var tails = ordered
                .Where(outcome => outcome.Counts.Max() >= 5 ||
                                  outcome.Counts.Count(value => value > 0) >= 5)
                .ToList();
            var pool = tails.Count >= limit
                ? tails
                : ordered.Where(outcome => !tails.Contains(outcome)).ToList();
            var selected = tails.Take(limit).ToList();
            int remaining = limit - selected.Count;
            for (int i = 0; i < remaining; i++)
            {
                int index = (int)((i + 0.5) * pool.Count / Math.Max(1, remaining));
                selected.Add(pool[index]);
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
        private readonly string _stateKey;
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
            _stateKey = BotExperienceKeys.State(game);
        }

        public double ExpectedValue(int[] counts, int rollNumber, bool allThrown) =>
            Evaluate(counts, rollNumber, allThrown);

        public MoveChoice? BestMove(int[] counts, int rollNumber, bool allThrown) =>
            BestMoveInternal(counts, rollNumber, allThrown);

        public bool IsPremiumWrite(
            AvailableMoveDto move,
            int[] counts,
            int rollNumber,
            bool allThrown)
        {
            var scores = RowScores(counts, rollNumber, _engine, _rules);
            return LockPremium(move, scores[(int)move.Row], rollNumber) > 0;
        }

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
                value += _owner.ExperienceBonus(BotDecisionKind.Hold, _stateKey, BotExperienceKeys.Hold(kept));
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
                .OrderByDescending(kept => _owner.TargetedHoldPriority(kept, _candidates, _stateKey))
                .ThenByDescending(Sum)
                .ThenBy(CountKey, StringComparer.Ordinal)
                .Take(HoldLimitFor(rollNumber))
                .ToList();

            if (!choices.Any(kept => Sum(kept) == total)) choices.Add((int[])counts.Clone());
            if (!choices.Any(kept => Sum(kept) == 0)) choices.Insert(0, new int[counts.Length]);
            return choices;
        }

        private double LockPremium(AvailableMoveDto move, int score, int rollNumber)
        {
            if (score <= 0) return 0;
            int maximum = _engine.GetMaximum(move.Row);
            if (move.Row == Row.Straight) return rollNumber == 1 ? 22 : 8;
            if (IsHighCeilingRow(move.Row) && score == maximum)
                return rollNumber == 1 ? 12 : 6;
            if (move.Row == Row.Ones && score == maximum && rollNumber == 1) return 5;
            return 0;
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
            if (rollNumber <= 2)
            {
                var immediate = BestMoveInternal(counts, rollNumber, allThrown);
                if (immediate is not null)
                {
                    var scores = RowScores(counts, rollNumber, _engine, _rules);
                    double premium = LockPremium(immediate.Move, scores[(int)immediate.Move.Row], rollNumber);
                    if (premium > 0)
                    {
                        double locked = immediate.Value + premium +
                            _owner.ExperienceBonus(BotDecisionKind.Move, _stateKey, BotExperienceKeys.Move(immediate.Move));
                        if (locked > best) best = locked;
                    }
                }
            }

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
                    value += _owner.ExperienceBonus(
                        BotDecisionKind.Move,
                        _stateKey,
                        BotExperienceKeys.Move(move));
                    if (best is null || value > best.Value)
                    best = new MoveChoice(value, move);
            }

            return best;
        }
    }
}
