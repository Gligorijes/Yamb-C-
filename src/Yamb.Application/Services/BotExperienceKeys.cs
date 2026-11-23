using System.Security.Cryptography;
using System.Text;
using Yamb.Application.DTOs;
using Yamb.Application.Models;
using Yamb.Domain.Enums;

namespace Yamb.Application.Services;

public static class BotExperienceKeys
{
    public static string State(GameDto game)
    {
        var sheet = game.Players[game.CurrentPlayerIndex].Scoresheet;
        var counts = Counts(game.Dice.Dice.Select(die => die.Value));
        int totalDice = counts.Sum();
        int maxCount = counts.Max();
        int maxFace = Array.IndexOf(counts, maxCount) + 1;
        int distinct = counts.Count(value => value > 0);
        int filled = sheet.FilledCells;
        double fill = sheet.TotalCells == 0 ? 0 : (double)filled / sheet.TotalCells;
        int fillBucket = Bucket(fill, 1, 10);

        var columnFill = string.Join('.', game.Columns.Select(column =>
            sheet.Cells.TryGetValue(column, out var cells)
                ? Bucket(cells.Values.Count(value => value.HasValue), Enum.GetValues<Row>().Length, 4)
                : 0));

        int upper = 0;
        foreach (var cells in sheet.Cells.Values)
            foreach (var row in Enum.GetValues<Row>())
                if (row is >= Row.Ones and <= Row.Sixes && cells.TryGetValue(row, out var value))
                    upper += value ?? 0;
        int upperBucket = Bucket(upper, 60, 6);

        int max = FindFilled(sheet, Row.Max) ?? 26;
        int min = FindFilled(sheet, Row.Min) ?? 9;
        int spreadBucket = Bucket(Math.Max(0, max - min), 40, 5);

        string announcement = game.AnnouncedRow switch
        {
            null => "-",
            Row.Yamb or Row.FourOfAKind or Row.FullHouse or Row.Straight or Row.ThreeOfAKind => "H",
            _ => "N"
        };

        var raw = new StringBuilder("v3|");
        raw.Append(string.Join(',', game.Columns)).Append('|');
        raw.Append(game.Dice.Dice.Count).Append('|');
        raw.Append(game.RollNumber).Append('|');
        raw.Append(game.AllDiceThrownThisRoll ? '1' : '0').Append('|');
        raw.Append(announcement).Append('|');
        raw.Append(fillBucket).Append('|');
        raw.Append(totalDice).Append('.').Append(maxCount).Append('.').Append(maxFace).Append('.').Append(distinct).Append('|');
        raw.Append(upperBucket).Append('|');
        raw.Append(spreadBucket).Append('|');
        raw.Append(columnFill);

        return Hash(raw.ToString());
    }

    public static string Move(AvailableMoveDto move) => $"M|{(int)move.Column}|{(int)move.Row}";

    public static string Hold(int[] keptCounts) => $"H|{string.Join('.', keptCounts)}";

    public static string Announcement(Row? row) => $"A|{(row.HasValue ? (int)row.Value : -1)}";

    public static string Stop(bool finish) => $"S|{(finish ? 1 : 0)}";

    public static string EntryKey(BotDecisionKind kind, string stateKey, string moveKey) =>
        $"{(int)kind}|{stateKey}|{moveKey}";

    private static int Bucket(double value, double maximum, int buckets) =>
        Math.Clamp((int)Math.Floor(value / Math.Max(maximum, 1.0) * buckets), 0, buckets - 1);

    private static int? FindFilled(ScoresheetDto sheet, Row row)
    {
        foreach (var cells in sheet.Cells.Values)
            if (cells.TryGetValue(row, out var value) && value.HasValue)
                return value.Value;
        return null;
    }

    private static int[] Counts(IEnumerable<int> values)
    {
        var counts = new int[6];
        foreach (int value in values)
            counts[Math.Clamp(value, 1, 6) - 1]++;
        return counts;
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
}
