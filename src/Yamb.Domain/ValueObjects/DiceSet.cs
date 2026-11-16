using Yamb.Domain.Exceptions;

namespace Yamb.Domain.ValueObjects;

public sealed class DiceSet
{
    public IReadOnlyList<Die> Dice { get; }

    public int Count => Dice.Count;

    public DiceSet(IReadOnlyList<Die> dice)
    {
        if (dice is null || dice.Count == 0)
            throw new DomainException("A dice set requires at least one die.");

        foreach (var die in dice)
        {
            if (die.Value < 1 || die.Value > 6)
                throw new DomainException($"Die value must be between 1 and 6. Got: {die.Value}");
        }

        Dice = dice;
    }

    public static DiceSet Initial(int diceCount) =>
        new(Enumerable.Repeat(new Die(1), diceCount).ToList());

    public DiceSet Reroll(Random rng) => new(Dice.Select(d => d.Reroll(rng)).ToList());

    public bool AllDiceThrown => Dice.All(d => !d.IsHeld);

    public IReadOnlyList<int> Values => Dice.Select(d => d.Value).OrderBy(v => v).ToList();
}
