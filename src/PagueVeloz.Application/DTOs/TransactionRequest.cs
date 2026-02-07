using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

public sealed record TransactionRequest
{
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = null!;

    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = null!;

    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;

    [JsonPropertyName("reference_id")]
    public string ReferenceId { get; init; } = null!;

    [JsonPropertyName("destination_account_id")]
    public string? DestinationAccountId { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}
