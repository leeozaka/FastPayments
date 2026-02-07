namespace PagueVeloz.Domain.ValueObjects;

public sealed record Money
{
    public long AmountInCents { get; }
    public Currency Currency { get; }

    private Money(long amountInCents, Currency currency)
    {
        AmountInCents = amountInCents;
        Currency = currency;
    }

    public static Money Create(long amountInCents, Currency currency)
    {
        if (amountInCents < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amountInCents));

        return new Money(amountInCents, currency);
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(AmountInCents + other.AmountInCents, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(AmountInCents - other.AmountInCents, Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return AmountInCents > other.AmountInCents;
    }

    public bool IsGreaterThanOrEqual(Money other)
    {
        EnsureSameCurrency(other);
        return AmountInCents >= other.AmountInCents;
    }

    public bool IsNegative() => AmountInCents < 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on different currencies: {Currency} and {other.Currency}.");
    }

    public override string ToString() => $"{Currency} {AmountInCents / 100m:N2}";
}
