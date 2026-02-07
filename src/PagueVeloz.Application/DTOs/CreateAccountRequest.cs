using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

public sealed record CreateAccountRequest
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = null!;

    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    [JsonPropertyName("initial_balance")]
    public long InitialBalance { get; init; }

    [JsonPropertyName("credit_limit")]
    public long CreditLimit { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "BRL";
}
