using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

public sealed record AccountResponse
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = null!;

    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = null!;

    [JsonPropertyName("balance")]
    public long Balance { get; init; }

    [JsonPropertyName("reserved_balance")]
    public long ReservedBalance { get; init; }

    [JsonPropertyName("available_balance")]
    public long AvailableBalance { get; init; }

    [JsonPropertyName("credit_limit")]
    public long CreditLimit { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;
}
