using FluentAssertions;
using Yamb.Domain.Enums;
using Yamb.Domain.Services;

namespace Yamb.Domain.Tests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    [Theory]
    [InlineData(Row.Ones, 1, 5)]
    [InlineData(Row.Twos, 2, 10)]
    [InlineData(Row.Threes, 3, 15)]
    [InlineData(Row.Fours, 4, 20)]
    [InlineData(Row.Fives, 5, 25)]
    [InlineData(Row.Sixes, 6, 30)]
    public void NumberRows_WithSixMatchingDice_CountAtMostFive(Row row, int face, int expected)
    {
        var dice = Enumerable.Repeat(face, 6).ToArray();

        _engine.Calculate(dice, row, 1).Should().Be(expected);
    }

    [Fact]
    public void Twos_WithThreeTwos_SumsMatchingDice()
    {
        _engine.Calculate(new[] { 2, 2, 2, 4, 5, 6 }, Row.Twos, 1).Should().Be(6);
    }

    [Fact]
    public void Max_SumsTopFiveDice()
    {
        _engine.Calculate(new[] { 1, 6, 6, 6, 6, 6 }, Row.Max, 1).Should().Be(30);
    }

    [Fact]
    public void Max_MixedDice_SumsTopFive()
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.Max, 1).Should().Be(20);
    }

    [Fact]
    public void Min_AllSixes_SumsBottomFive()
    {
        _engine.Calculate(new[] { 6, 6, 6, 6, 6, 6 }, Row.Min, 1).Should().Be(30);
    }

    [Fact]
    public void Min_AllOnes_SumsBottomFive()
    {
        _engine.Calculate(new[] { 1, 1, 1, 1, 1, 1 }, Row.Min, 1).Should().Be(5);
    }

    [Fact]
    public void Min_MixedDice_SumsBottomFive()
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.Min, 1).Should().Be(15);
    }

    [Theory]
    [InlineData(1, 66)]
    [InlineData(2, 56)]
    [InlineData(3, 46)]
    public void Straight_PaysByRollNumber(int rollNumber, int expected)
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.Straight, rollNumber).Should().Be(expected);
    }

    [Fact]
    public void Straight_HighStraight_IsValid()
    {
        _engine.Calculate(new[] { 2, 3, 4, 5, 6, 2 }, Row.Straight, 1).Should().Be(66);
    }

    [Fact]
    public void Straight_InvalidSequence_ScoresZero()
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 6, 6 }, Row.Straight, 1).Should().Be(0);
    }

    [Fact]
    public void ThreeOfAKind_HighestTriple()
    {
        _engine.Calculate(new[] { 6, 6, 6, 3, 3, 1 }, Row.ThreeOfAKind, 1).Should().Be(38);
    }

    [Fact]
    public void ThreeOfAKind_TwoTriples_PicksHighestFace()
    {
        _engine.Calculate(new[] { 5, 5, 5, 6, 6, 6 }, Row.ThreeOfAKind, 1).Should().Be(38);
    }

    [Fact]
    public void ThreeOfAKind_None_ScoresZero()
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.ThreeOfAKind, 1).Should().Be(0);
    }

    [Fact]
    public void FullHouse_ValidCombo_CorrectScore()
    {
        _engine.Calculate(new[] { 4, 4, 4, 5, 5, 1 }, Row.FullHouse, 1).Should().Be(52);
    }

    [Fact]
    public void FullHouse_PicksBestCombination_FromFourPlusPair()
    {
        _engine.Calculate(new[] { 6, 6, 6, 6, 5, 5 }, Row.FullHouse, 1).Should().Be(58);
    }

    [Fact]
    public void FullHouse_FiveOfAKind_ScoresZero()
    {
        _engine.Calculate(new[] { 5, 5, 5, 5, 5, 2 }, Row.FullHouse, 1).Should().Be(0);
    }

    [Fact]
    public void FourOfAKind_CorrectScore()
    {
        _engine.Calculate(new[] { 4, 4, 4, 4, 2, 2 }, Row.FourOfAKind, 1).Should().Be(56);
    }

    [Fact]
    public void FourOfAKind_SixDice_PicksHighestFace()
    {
        _engine.Calculate(new[] { 6, 6, 6, 6, 6, 6 }, Row.FourOfAKind, 1).Should().Be(64);
    }

    [Fact]
    public void Yamb_FiveOfAKind_CorrectScore()
    {
        _engine.Calculate(new[] { 6, 6, 6, 6, 6, 1 }, Row.Yamb, 1).Should().Be(80);
    }

    [Fact]
    public void Yamb_None_ScoresZero()
    {
        _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.Yamb, 1).Should().Be(0);
    }

    [Fact]
    public void Calculate_WrongDiceCount_Throws()
    {
        var act = () => _engine.Calculate(new[] { 1, 2, 3, 4, 5 }, Row.Ones, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Calculate_InvalidRollNumber_Throws()
    {
        var act = () => _engine.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, Row.Ones, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NumbersBonus_AboveThreshold_AddsBonus()
    {
        var cells = Cells((Row.Ones, 5), (Row.Twos, 10), (Row.Threes, 15),
                          (Row.Fours, 20), (Row.Fives, 5), (Row.Sixes, 6));
        _engine.CalculateNumbersBonus(cells).Should().Be(91);
    }

    [Fact]
    public void NumbersBonus_ExactlyAtThreshold_AddsBonus()
    {
        var cells = Cells((Row.Ones, 5), (Row.Twos, 10), (Row.Threes, 15),
                          (Row.Fours, 20), (Row.Fives, 5), (Row.Sixes, 5));
        _engine.CalculateNumbersBonus(cells).Should().Be(90);
    }

    [Fact]
    public void NumbersBonus_BelowThreshold_NoBonus()
    {
        var cells = Cells((Row.Ones, 5), (Row.Twos, 10), (Row.Threes, 15),
                          (Row.Fours, 20), (Row.Fives, 5), (Row.Sixes, 0));
        _engine.CalculateNumbersBonus(cells).Should().Be(55);
    }

    [Fact]
    public void NumbersBonus_Incomplete_IsNull()
    {
        var cells = Cells((Row.Ones, 5), (Row.Twos, 10), (Row.Threes, 15),
                          (Row.Fours, 20), (Row.Fives, 5));
        _engine.CalculateNumbersBonus(cells).Should().BeNull();
    }

    [Fact]
    public void MaxMinBonus_CorrectFormula()
    {
        var cells = Cells((Row.Max, 28), (Row.Min, 8), (Row.Ones, 3));
        _engine.CalculateMaxMinBonus(cells).Should().Be(60);
    }

    [Fact]
    public void MaxMinBonus_Incomplete_IsNull()
    {
        var cells = Cells((Row.Max, 28), (Row.Min, 8));
        _engine.CalculateMaxMinBonus(cells).Should().BeNull();
    }

    [Fact]
    public void CombinationsBonus_SumsAllComboRows()
    {
        var cells = Cells((Row.Straight, 66), (Row.ThreeOfAKind, 38), (Row.FullHouse, 58),
                          (Row.FourOfAKind, 64), (Row.Yamb, 80));
        _engine.CalculateCombinationsBonus(cells).Should().Be(306);
    }

    [Fact]
    public void CombinationsBonus_Incomplete_IsNull()
    {
        var cells = Cells((Row.Straight, 66), (Row.ThreeOfAKind, 38), (Row.FullHouse, 58),
                          (Row.FourOfAKind, 64));
        _engine.CalculateCombinationsBonus(cells).Should().BeNull();
    }

    [Theory]
    [InlineData(Row.Ones, 5)]
    [InlineData(Row.Twos, 10)]
    [InlineData(Row.Threes, 15)]
    [InlineData(Row.Fours, 20)]
    [InlineData(Row.Fives, 25)]
    [InlineData(Row.Sixes, 30)]
    [InlineData(Row.Max, 30)]
    [InlineData(Row.Min, 5)]
    [InlineData(Row.Straight, 66)]
    [InlineData(Row.ThreeOfAKind, 38)]
    [InlineData(Row.FullHouse, 58)]
    [InlineData(Row.FourOfAKind, 64)]
    [InlineData(Row.Yamb, 80)]
    public void GetMaximum_ReturnsHighestPossibleRowScore(Row row, int expected)
    {
        _engine.GetMaximum(row).Should().Be(expected);
    }

    private static IReadOnlyDictionary<Row, int?> Cells(params (Row Row, int Value)[] filled)
    {
        var cells = Enum.GetValues<Row>().ToDictionary(r => r, _ => (int?)null);
        foreach (var (row, value) in filled)
        {
            cells[row] = value;
        }
        return cells;
    }
}
