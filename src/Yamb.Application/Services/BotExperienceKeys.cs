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
        var raw = new StringBuilder("v2|");
        raw.Append(string.Join(',', game.Columns)).Append('|');
        raw.Append(game.Dice.Dice.Count).Append('|');
        raw.Append(game.RollNumber).Append('|');
        raw.Append(game.AllDiceThrownThisRoll ? '1' : '0').Append('|');
        raw.Append(game.AnnouncedRow?.ToString() ?? "-").Append('|');
        raw.Append(string.Join('.', Counts(game.Dice.Dice.Select(die => die.Value)))).Append('|');

        foreach (var column in game.Columns)
        {
            raw.Append(column).Append(':');
            if (sheet.Cells.TryGetValue(column, out var cells))
            {
                foreach (var row in Enum.GetValues<Row>())
                {
                    raw.Append(cells.TryGetValue(row, out var value) && value.HasValue ? value.Value : -999);
                    raw.Append(',');
                }
            }
            raw.Append('|');
        }

        return Hash(raw.ToString());
    }

    public static string Move(AvailableMoveDto move) => $"M|{(int)move.Column}|{(int)move.Row}";

    public static string Hold(int[] keptCounts) => $"H|{string.Join('.', keptCounts)}";

    public static string Announcement(Row? row) => $"A|{(row.HasValue ? (int)row.Value : -1)}";

    public static string EntryKey(BotDecisionKind kind, string stateKey, string moveKey) =>
        $"{(int)kind}|{stateKey}|{moveKey}";

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
