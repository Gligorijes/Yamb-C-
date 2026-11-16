using FluentAssertions;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;

namespace Yamb.Domain.Tests;

public class SheetDefinitionsTests
{
    [Theory]
    [InlineData(SheetVariant.Four, 4)]
    [InlineData(SheetVariant.Six, 6)]
    [InlineData(SheetVariant.Seven, 7)]
    [InlineData(SheetVariant.Eight, 8)]
    [InlineData(SheetVariant.Nine, 9)]
    [InlineData(SheetVariant.Ten, 10)]
    public void GetColumns_ReturnsExpectedCount(SheetVariant variant, int expected)
    {
        SheetDefinitions.GetColumns(variant).Should().HaveCount(expected);
    }

    [Fact]
    public void DefaultColumns_AreSevenColumnSheet()
    {
        SheetDefinitions.DefaultColumns.Should().Equal(SheetDefinitions.GetColumns(SheetVariant.Seven));
    }

    [Theory]
    [InlineData(SheetVariant.Four)]
    [InlineData(SheetVariant.Six)]
    [InlineData(SheetVariant.Seven)]
    [InlineData(SheetVariant.Eight)]
    [InlineData(SheetVariant.Nine)]
    [InlineData(SheetVariant.Ten)]
    public void GetColumns_AreDistinctAndAlwaysIncludeAnnouncement(SheetVariant variant)
    {
        var columns = SheetDefinitions.GetColumns(variant);

        columns.Should().OnlyHaveUniqueItems();
        columns.Should().Contain(Column.Announcement);
    }

    [Fact]
    public void TenColumnSheet_ContainsEveryColumn()
    {
        SheetDefinitions.GetColumns(SheetVariant.Ten)
            .Should().BeEquivalentTo(Enum.GetValues<Column>());
    }
}
