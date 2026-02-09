using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

/// <summary>
/// Represents a request to process a transaction.
/// </summary>
public sealed record TransactionRequest
{
    /// <summary>
    /// The operation type: credit, debit, or transfer.
    /// </summary>
    /// <example>credit</example>
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = null!;

    /// <summary>
    /// The source account ID for the transaction.
    /// </summary>
    /// <example>acc_123456</example>
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = null!;

    /// <summary>
    /// The transaction amount in cents.
    /// </summary>
    /// <example>1000</example>
    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    /// <summary>
    /// The currency code (ISO 4217).
    /// </summary>
    /// <example>BRL</example>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;

    /// <summary>
    /// A unique reference ID for idempotency and tracking.
    /// </summary>
    /// <example>ref_abc123</example>
    [JsonPropertyName("reference_id")]
    public string ReferenceId { get; init; } = null!;

    /// <summary>
    /// The destination account ID. Required for transfer operations.
    /// </summary>
    /// <example>acc_654321</example>
    [JsonPropertyName("destination_account_id")]
    public string? DestinationAccountId { get; init; }

    /// <summary>
    /// Additional metadata for the transaction.
    /// </summary>
    /// <example>{"category": "salary", "description": "Monthly payment"}</example>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}
