using FluentAssertions;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_normalizes_currency_code_to_upper_case()
    {
        var money = new Money(19.99m, "bgn");

        money.CurrencyCode.Should().Be("BGN");
    }

    [Fact]
    public void Constructor_rejects_negative_amount()
    {
        var act = () => new Money(-1m, "USD");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void Constructor_rejects_a_currency_code_that_is_not_3_letters(string currencyCode)
    {
        var act = () => new Money(1m, currencyCode);

        act.Should().Throw<ArgumentException>();
    }
}
