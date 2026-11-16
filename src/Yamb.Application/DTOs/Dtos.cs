using Yamb.Domain.Enums;

namespace Yamb.Application.DTOs;

public record DieDto(int Value, bool IsHeld);

public record DiceSetDto(IReadOnlyList<DieDto> Dice);

public record BonusesDto(int? NumbersBonus, int? MaxMinBonus, int? CombinationsBonus);

public record ScoresheetDto(
    IReadOnlyDictionary<Column, IReadOnlyDictionary<Row, int?>> Cells,
    IReadOnlyDictionary<Column, BonusesDto> Bonuses,
    IReadOnlyDictionary<Row, int?> RowTotals,
    BonusesDto BonusTotals,
    IReadOnlyDictionary<Column, int> ColumnTotals,
    int CurrentScore,
    int FilledCells,
    int TotalCells,
    int? FinalScore);

public record PlayerDto(string Name, ScoresheetDto Scoresheet);

public record GameDto(
    Guid Id,
    GameMode Mode,
    IReadOnlyList<Column> Columns,
    IReadOnlyList<PlayerDto> Players,
    int CurrentPlayerIndex,
    DiceSetDto Dice,
    int RollNumber,
    Row? AnnouncedRow,
    bool AllDiceThrownThisRoll,
    bool IsGameOver,
    GameResult Result);

public record AvailableMoveDto(Column Column, Row Row, int PreviewScore);

public record PlayerScoreDto(string Name, int Score);

public record LeaderboardEntryDto(int Rank, string Participants, int Score, DateTime AchievedAt);

public record GameSummaryDto(Guid Id, GameMode Mode, string PlayerNames, DateTime SavedAt);

public record StartGameCommand(
    GameMode Mode,
    IReadOnlyList<string> PlayerNames,
    SheetVariant Variant = SheetVariant.Seven,
    int DiceCount = 6);
