using Yamb.Domain.Enums;
using Yamb.Domain.Exceptions;
using Yamb.Domain.Rules;

namespace Yamb.Domain.Aggregates;

public sealed class Player
{
    public string Name { get; }

    public Scoresheet Scoresheet { get; }

    public Player(string name, GameRuleset? rules = null, IReadOnlyList<Column>? columns = null)
    {
        Name = NormalizeName(name);
        Scoresheet = new Scoresheet(rules, columns);
    }

    private Player(string name, Scoresheet scoresheet)
    {
        Name = name;
        Scoresheet = scoresheet;
    }

    public static Player Restore(string name, Scoresheet scoresheet) =>
        new(NormalizeName(name), scoresheet);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Player name cannot be empty.");
        return name.Trim();
    }
}
