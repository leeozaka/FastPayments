using FluentAssertions;
using PagueVeloz.Domain.ValueObjects;
using Xunit;

namespace PagueVeloz.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidValues_ShouldSucceed()
    {
        var money = Money.Create(1000, Currency.BRL);

        money.AmountInCents.Should().Be(1000);
        money.Currency.Should().Be(Currency.BRL);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Money.Create(-1, Currency.BRL);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = Money.Create(500, Currency.BRL);
        var b = Money.Create(300, Currency.BRL);

        var result = a.Add(b);

        result.AmountInCents.Should().Be(800);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var a = Money.Create(500, Currency.BRL);
        var b = Money.Create(300, Currency.USD);

        var act = () => a.Add(b);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_ShouldReturnDifference()
    {
        var a = Money.Create(500, Currency.BRL);
        var b = Money.Create(300, Currency.BRL);

        var result = a.Subtract(b);

        result.AmountInCents.Should().Be(200);
    }

    [Fact]
    public void IsGreaterThan_ShouldCompareCorrectly()
    {
        var a = Money.Create(500, Currency.BRL);
        var b = Money.Create(300, Currency.BRL);

        a.IsGreaterThan(b).Should().BeTrue();
        b.IsGreaterThan(a).Should().BeFalse();
    }
}
