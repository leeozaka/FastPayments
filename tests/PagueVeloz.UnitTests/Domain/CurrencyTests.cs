using FluentAssertions;
using PagueVeloz.Domain.ValueObjects;
using Xunit;

namespace PagueVeloz.UnitTests.Domain;

public class CurrencyTests
{
    [Theory]
    [InlineData("BRL")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Create_SupportedCurrency_ShouldSucceed(string code)
    {
        var currency = Currency.Create(code);

        currency.Code.Should().Be(code);
    }

    [Theory]
    [InlineData("brl", "BRL")]
    [InlineData("usd", "USD")]
    [InlineData("Eur", "EUR")]
    public void Create_LowercaseInput_ShouldNormalizeToUppercase(string input, string expected)
    {
        var currency = Currency.Create(input);

        currency.Code.Should().Be(expected);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("XYZ")]
    public void Create_UnsupportedCurrency_ShouldThrow(string code)
    {
        var act = () => Currency.Create(code);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported currency*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyOrWhitespace_ShouldThrow(string? code)
    {
        var act = () => Currency.Create(code!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Currency code is required*");
    }

    [Fact]
    public void Equality_SameCurrency_ShouldBeEqual()
    {
        var a = Currency.Create("BRL");
        var b = Currency.Create("BRL");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentCurrency_ShouldNotBeEqual()
    {
        var a = Currency.Create("BRL");
        var b = Currency.Create("USD");

        a.Should().NotBe(b);
    }

    [Fact]
    public void StaticProperties_ShouldReturnCorrectCode()
    {
        Currency.BRL.Code.Should().Be("BRL");
        Currency.USD.Code.Should().Be("USD");
    }

    [Fact]
    public void ToString_ShouldReturnCode()
    {
        var currency = Currency.Create("BRL");

        currency.ToString().Should().Be("BRL");
    }

    [Fact]
    public void Create_WithLeadingTrailingWhitespace_ShouldTrim()
    {
        var currency = Currency.Create("  BRL  ");

        currency.Code.Should().Be("BRL");
    }
}
