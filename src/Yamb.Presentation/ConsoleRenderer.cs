using Yamb.Application.DTOs;
using Yamb.Domain.Enums;

namespace Yamb.Presentation;

public static class ConsoleRenderer
{
    private static readonly (Column Column, string Label)[] Columns =
    {
        (Column.Downward, "D"),
        (Column.Free, "F"),
        (Column.Upward, "U"),
        (Column.Announcement, "A"),
        (Column.Hand, "H"),
        (Column.BothEnds, "B"),
        (Column.MiddleOut, "M")
    };

    private static readonly Row[] RowOrder =
    {
        Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes,
        Row.Max, Row.Min, Row.Straight, Row.ThreeOfAKind, Row.FullHouse,
        Row.FourOfAKind, Row.Yamb
    };

    public static string RowLabel(Row row) => row switch
    {
        Row.Ones => "1s",
        Row.Twos => "2s",
        Row.Threes => "3s",
        Row.Fours => "4s",
        Row.Fives => "5s",
        Row.Sixes => "6s",
        Row.Max => "Max",
        Row.Min => "Min",
        Row.Straight => "St",
        Row.ThreeOfAKind => "3k",
        Row.FullHouse => "FH",
        Row.FourOfAKind => "4k",
        Row.Yamb => "Ya",
        _ => "?"
    };

    public static string ColumnLabel(Column column) =>
        Columns.First(c => c.Column == column).Label;

    public static string ColumnName(Column column) => column switch
    {
        Column.Downward => "D (Downward)",
        Column.Free => "F (Free)",
        Column.Upward => "U (Upward)",
        Column.Announcement => "A (Announce)",
        Column.Hand => "H (Hand)",
        Column.BothEnds => "B (BothEnds)",
        Column.MiddleOut => "M (MiddleOut)",
        _ => column.ToString()
    };

    public static void Render(GameDto game)
    {
        SafeClear();
        var player = game.Players[game.CurrentPlayerIndex];

        Console.WriteLine($"=== {game.Mode} | Turn: {player.Name} | Roll {game.RollNumber}/3 ===");
        Console.WriteLine();

        if (game.AnnouncedRow is not null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Announced: {RowLabel(game.AnnouncedRow.Value)} - you MUST write it in column A this turn.");
            Console.ResetColor();
        }

        RenderSheet(player.Scoresheet);
        RenderDice(game);

        Console.WriteLine();
        Console.WriteLine("Columns: D=Down  F=Free  U=Up  A=Announce  H=Hand  B=BothEnds  M=MiddleOut");
        Console.WriteLine("Commands: r=roll  h<n>=hold die n  a=announce  w=write score  ?=help  q=quit");
    }

    private static void RenderSheet(ScoresheetDto sheet)
    {
        Console.Write(new string(' ', 5));
        foreach (var (_, label) in Columns)
        {
            Console.Write($"{label,4}");
        }
        Console.WriteLine();

        foreach (var row in RowOrder)
        {
            Console.Write($"{RowLabel(row),5}");
            foreach (var (column, _) in Columns)
            {
                var cell = sheet.Cells[column].TryGetValue(row, out var v) && v.HasValue
                    ? v.Value.ToString()
                    : ".";
                Console.Write($"{cell,4}");
            }
            Console.WriteLine();
        }

        RenderBonusRow("Num", sheet, b => b.NumbersBonus);
        RenderBonusRow("M+M", sheet, b => b.MaxMinBonus);
        RenderBonusRow("Cmb", sheet, b => b.CombinationsBonus);
        RenderColumnTotals(sheet);

        Console.WriteLine($"{"TOTAL",5}  {sheet.FinalScore?.ToString() ?? "--"}");
    }

    private static void RenderBonusRow(string label, ScoresheetDto sheet, Func<BonusesDto, int?> selector)
    {
        Console.Write($"{label,5}");
        foreach (var (column, _) in Columns)
        {
            var value = selector(sheet.Bonuses[column]);
            Console.Write($"{(value.HasValue ? value.Value.ToString() : "."),4}");
        }
        Console.WriteLine();
    }

    private static void RenderColumnTotals(ScoresheetDto sheet)
    {
        Console.Write($"{"Sum",5}");
        foreach (var (column, _) in Columns)
        {
            var bonuses = sheet.Bonuses[column];
            int? total = bonuses.NumbersBonus.HasValue && bonuses.MaxMinBonus.HasValue && bonuses.CombinationsBonus.HasValue
                ? bonuses.NumbersBonus.Value + bonuses.MaxMinBonus.Value + bonuses.CombinationsBonus.Value
                : null;
            Console.Write($"{(total.HasValue ? total.Value.ToString() : "."),4}");
        }
        Console.WriteLine();
    }

    private static void RenderDice(GameDto game)
    {
        Console.WriteLine();
        Console.Write("Dice: ");
        for (int i = 0; i < game.Dice.Dice.Count; i++)
        {
            var die = game.Dice.Dice[i];
            Console.Write(die.IsHeld ? $"[{die.Value}]'" : $"[{die.Value}]");
            Console.Write(' ');
        }
        Console.WriteLine("  (' = held)");
    }

    public static void RenderRules()
    {
        SafeClear();
        Console.WriteLine(
"""
YAMB - HOW TO PLAY

Each turn: roll up to 3 times, hold dice between rolls, then write ONE score.
Only 5 of the 6 dice count toward every score.

ROWS:
  1s..6s   sum of ALL matching dice
  Max      sum of the 5 highest dice
  Min      sum of the 5 lowest dice
  St       straight 1-5 or 2-6: 66 / 56 / 46 on 1st / 2nd / 3rd roll
  3k       three of a kind: 3 x face + 20
  FH       full house (3+2, different faces): 3xf1 + 2xf2 + 30
  4k       four of a kind: 4 x face + 40
  Ya       yamb (five of a kind): 5 x face + 50

COLUMNS:
  D  Downward   fill top to bottom, strictly in order
  F  Free       fill any empty cell
  U  Upward     fill bottom to top, strictly in order
  A  Announce   declare a row after roll 1; you MUST write it that turn
  H  Hand       valid only if all 6 dice were re-rolled
  B  BothEnds   fill from the top down or the bottom up
  M  MiddleOut  fill from Max upward or from Min downward

BONUSES (per column):
  Num  numbers 1-6 sum + 30 if >= 60
  M+M  (Max - Min) x Ones
  Cmb  sum of St + 3k + FH + 4k + Ya

        Final score = sum of all column totals.
""");
    }

    public static void SafeClear()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
        }
    }
}
