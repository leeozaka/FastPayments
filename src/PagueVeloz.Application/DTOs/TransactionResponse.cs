using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

/// <summary>
/// Represents the response of a transaction.
/// </summary>
public sealed record TransactionResponse
{
    /// <summary>
    /// The unique identifier of the transaction.
    /// </summary>
    /// <example>txn_12345</example>
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; init; } = null!;

    /// <summary>
    /// The status of the transaction (e.g., success, failed, pending).
    /// </summary>
    /// <example>success</example>
    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    /// <summary>
    /// The current balance after the transaction (in cents).
    /// </summary>
    /// <example>10000</example>
    [JsonPropertyName("balance")]
    public long Balance { get; init; }

    /// <summary>
    /// The amount of funds currently reserved for pending transactions (in cents).
    /// </summary>
    /// <example>0</example>
    [JsonPropertyName("reserved_balance")]
    public long ReservedBalance { get; init; }

    /// <summary>
    /// The amount of funds available for new transactions (in cents).
    /// </summary>
    /// <example>10000</example>
    [JsonPropertyName("available_balance")]
    public long AvailableBalance { get; init; }

    /// <summary>
    /// The timestamp when the transaction was processed.
    /// </summary>
    /// <example>2024-01-15T10:30:00Z</example>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// The error message if the transaction failed. Null if successful.
    /// </summary>
    /// <example>Insufficient funds</example>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The credit transaction ID for transfers. Only present on debit side of a transfer.
    /// </summary>
    /// <example>txn_67890</example>
    [JsonPropertyName("credit_transaction_id")]
    public string? CreditTransactionId { get; init; }
}
