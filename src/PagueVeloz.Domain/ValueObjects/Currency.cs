namespace PagueVeloz.Domain.ValueObjects;

public sealed record Currency
{
    public string Code { get; }

    private static readonly HashSet<string> SupportedCurrencies = ["BRL", "USD", "EUR"];

    private Currency(string code) => Code = code;

    public static Currency Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Currency code is required.", nameof(code));

        var normalized = code.Trim().ToUpperInvariant();

        if (!SupportedCurrencies.Contains(normalized))
            throw new ArgumentException($"Unsupported currency: {normalized}.", nameof(code));

        return new Currency(normalized);
    }

    public static Currency BRL => new("BRL");
    public static Currency USD => new("USD");

    public override string ToString() => Code;
}
