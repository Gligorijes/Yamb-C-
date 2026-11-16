using Yamb.Domain.Enums;

namespace Yamb.Domain.Services;

public sealed record TurnContext(
    RollPhase Phase,
    Row? AnnouncedRow,
    bool AllDiceThrownThisRoll,
    Row? DirectedRow = null,
    bool ObligatoryUnlocked = false);
