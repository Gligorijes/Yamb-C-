namespace Yamb.Domain.ValueObjects;

public sealed record Die(int Value, bool IsHeld = false)
{
    public static Die Roll(Random rng) => new(rng.Next(1, 7));

    public Die WithHeld(bool held) => this with { IsHeld = held };

    public Die Reroll(Random rng) => IsHeld ? this : Roll(rng);
}
