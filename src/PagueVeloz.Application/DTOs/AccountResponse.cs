using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

/// <summary>
/// Represents the response containing account details.
/// </summary>
public sealed record AccountResponse
{
    /// <summary>
    /// The unique identifier of the account.
    /// </summary>
    /// <example>1234567890</example>
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = null!;

    /// <summary>
    /// The client identifier associated with the account.
    /// </summary>
    /// <example>12345678901</example>
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = null!;

    /// <summary>
    /// The current balance of the account (in cents).
    /// </summary>
    /// <example>1000</example>
    [JsonPropertyName("balance")]
    public long Balance { get; init; }

    /// <summary>
    /// The amount reserved from the balance (in cents).
    /// </summary>
    /// <example>0</example>
    [JsonPropertyName("reserved_balance")]
    public long ReservedBalance { get; init; }

    /// <summary>
    /// The available balance for transactions (in cents).
    /// </summary>
    /// <example>1000</example>
    [JsonPropertyName("available_balance")]
    public long AvailableBalance { get; init; }

    /// <summary>
    /// The credit limit of the account (in cents).
    /// </summary>
    /// <example>5000</example>
    [JsonPropertyName("credit_limit")]
    public long CreditLimit { get; init; }

    /// <summary>
    /// The status of the account.
    /// </summary>
    /// <example>active</example>
    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    /// <summary>
    /// The currency of the account.
    /// </summary>
    /// <example>BRL</example>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;
}
