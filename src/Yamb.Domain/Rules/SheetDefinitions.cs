using Yamb.Domain.Enums;

namespace Yamb.Domain.Rules;

public static class SheetDefinitions
{
    private static readonly IReadOnlyList<Column> Four = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Free, Column.Upward, Column.Announcement
    });

    private static readonly IReadOnlyList<Column> Six = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Upward, Column.Free, Column.Announcement,
        Column.Hand, Column.Directed
    });

    private static readonly IReadOnlyList<Column> Seven = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Free, Column.Upward, Column.Announcement,
        Column.Hand, Column.BothEnds, Column.MiddleOut
    });

    private static readonly IReadOnlyList<Column> Eight = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Upward, Column.Free, Column.Announcement,
        Column.Hand, Column.Directed, Column.Obligatory, Column.Maximum
    });

    private static readonly IReadOnlyList<Column> Nine = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Upward, Column.Free, Column.Announcement,
        Column.Hand, Column.Directed, Column.BothEnds, Column.Obligatory, Column.Maximum
    });

    private static readonly IReadOnlyList<Column> Ten = Array.AsReadOnly(new[]
    {
        Column.Downward, Column.Upward, Column.Free, Column.Announcement,
        Column.Hand, Column.Directed, Column.BothEnds, Column.MiddleOut,
        Column.Obligatory, Column.Maximum
    });

    public static IReadOnlyList<Column> DefaultColumns => Seven;

    public static IReadOnlyList<Column> GetColumns(SheetVariant variant) => variant switch
    {
        SheetVariant.Four => Four,
        SheetVariant.Six => Six,
        SheetVariant.Seven => Seven,
        SheetVariant.Eight => Eight,
        SheetVariant.Nine => Nine,
        SheetVariant.Ten => Ten,
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };
}
