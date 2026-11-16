using Yamb.Domain.Enums;
using Yamb.Domain.ValueObjects;

namespace Yamb.Domain.Snapshots;

public sealed record CellSnapshot(Column Column, Row Row, int? Value);

public sealed record PlayerSnapshot(string Name, IReadOnlyList<CellSnapshot> Cells);

public sealed record GameSnapshot(
    Guid Id,
    GameMode Mode,
    int CurrentPlayerIndex,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<Die> Dice,
    int RollNumber,
    Row? AnnouncedRow,
    bool AllDiceThrownThisRoll,
    IReadOnlyList<Column>? Columns = null,
    Row? LastAnnouncementRow = null,
    int? LastAnnouncementPlayerIndex = null,
    int DiceCount = 6);
