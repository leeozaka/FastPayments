namespace PagueVeloz.Application.Interfaces;

public static class CacheKeys
{
    private const string BalancePrefix = "balance:";
    private const string IdempotencyPrefix = "idempotency:";
    private const string AccountExistsPrefix = "account:exists:";

    public static string AccountBalance(string accountId) => $"{BalancePrefix}{accountId}";
    public static string Idempotency(string referenceId) => $"{IdempotencyPrefix}{referenceId}";
    public static string AccountExists(string accountId) => $"{AccountExistsPrefix}{accountId}";
}
