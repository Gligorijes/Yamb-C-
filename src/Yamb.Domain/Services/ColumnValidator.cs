using Yamb.Domain.Enums;

namespace Yamb.Domain.Services;

public static class ColumnValidator
{
    private static readonly Row[] TopToBottom =
    {
        Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes,
        Row.Max, Row.Min, Row.Straight, Row.ThreeOfAKind, Row.FullHouse,
        Row.FourOfAKind, Row.Yamb
    };

    private static readonly Row[] BottomToTop = TopToBottom.Reverse().ToArray();

    private static readonly Row[] MiddleUp =
    {
        Row.Max, Row.Sixes, Row.Fives, Row.Fours, Row.Threes, Row.Twos, Row.Ones
    };

    private static readonly Row[] MiddleDown =
    {
        Row.Min, Row.Straight, Row.ThreeOfAKind, Row.FullHouse, Row.FourOfAKind, Row.Yamb
    };

    public static IReadOnlySet<Row> GetAvailableRows(
        Column column,
        IReadOnlyDictionary<Row, int?> cells,
        TurnContext context)
    {
        return column switch
        {
            Column.Downward => ToSet(NextUnfilled(TopToBottom, cells)),
            Column.Upward => ToSet(NextUnfilled(BottomToTop, cells)),
            Column.Free => GetAllEmpty(cells),
            Column.Announcement => GetAnnouncementRows(cells, context),
            Column.Hand => GetHandRows(cells, context),
            Column.BothEnds => GetBothEndsRows(cells),
            Column.MiddleOut => GetMiddleOutRows(cells),
            Column.Directed => GetDirectedRows(cells, context),
            Column.Obligatory => GetObligatoryRows(cells, context),
            Column.Maximum => GetAllEmpty(cells),
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };
    }

    private static Row? NextUnfilled(Row[] sequence, IReadOnlyDictionary<Row, int?> cells)
    {
        foreach (var row in sequence)
        {
            if (!IsFilled(cells, row)) return row;
        }
        return null;
    }

    private static bool IsFilled(IReadOnlyDictionary<Row, int?> cells, Row row) =>
        cells.TryGetValue(row, out var v) && v.HasValue;

    private static IReadOnlySet<Row> ToSet(Row? row) =>
        row.HasValue ? new HashSet<Row> { row.Value } : new HashSet<Row>();

    private static IReadOnlySet<Row> GetAllEmpty(IReadOnlyDictionary<Row, int?> cells) =>
        Enum.GetValues<Row>()
            .Where(r => !IsFilled(cells, r))
            .ToHashSet();

    private static IReadOnlySet<Row> GetAnnouncementRows(
        IReadOnlyDictionary<Row, int?> cells, TurnContext context)
    {
        if (context.AnnouncedRow is null) return new HashSet<Row>();

        var row = context.AnnouncedRow.Value;
        return !IsFilled(cells, row) ? new HashSet<Row> { row } : new HashSet<Row>();
    }

    private static IReadOnlySet<Row> GetHandRows(
        IReadOnlyDictionary<Row, int?> cells, TurnContext context)
    {
        if (!context.AllDiceThrownThisRoll) return new HashSet<Row>();
        return GetAllEmpty(cells);
    }

    private static IReadOnlySet<Row> GetBothEndsRows(IReadOnlyDictionary<Row, int?> cells)
    {
        var available = new HashSet<Row>();
        var top = NextUnfilled(TopToBottom, cells);
        var bottom = NextUnfilled(BottomToTop, cells);

        if (top.HasValue) available.Add(top.Value);
        if (bottom.HasValue) available.Add(bottom.Value);
        return available;
    }

    private static IReadOnlySet<Row> GetMiddleOutRows(IReadOnlyDictionary<Row, int?> cells)
    {
        var available = new HashSet<Row>();
        var up = NextUnfilled(MiddleUp, cells);
        var down = NextUnfilled(MiddleDown, cells);

        if (up.HasValue) available.Add(up.Value);
        if (down.HasValue) available.Add(down.Value);
        return available;
    }

    private static IReadOnlySet<Row> GetDirectedRows(
        IReadOnlyDictionary<Row, int?> cells, TurnContext context)
    {
        if (context.DirectedRow is null) return new HashSet<Row>();

        var row = context.DirectedRow.Value;
        return !IsFilled(cells, row) ? new HashSet<Row> { row } : new HashSet<Row>();
    }

    private static IReadOnlySet<Row> GetObligatoryRows(
        IReadOnlyDictionary<Row, int?> cells, TurnContext context)
    {
        if (!context.ObligatoryUnlocked) return new HashSet<Row>();
        return ToSet(NextUnfilled(TopToBottom, cells));
    }
}
