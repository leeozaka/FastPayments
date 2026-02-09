using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

public sealed record TransactionResponse
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; init; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("balance")]
    public long Balance { get; init; }

    [JsonPropertyName("reserved_balance")]
    public long ReservedBalance { get; init; }

    [JsonPropertyName("available_balance")]
    public long AvailableBalance { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("credit_transaction_id")]
    public string? CreditTransactionId { get; init; }
}
