using System.Text.Json.Serialization;

namespace PagueVeloz.Application.DTOs;

/// <summary>
/// Represents a request to create a new account.
/// </summary>
public sealed record CreateAccountRequest
{
    /// <summary>
    /// The client identifier associated with the account.
    /// </summary>
    /// <example>12345678901</example>
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = null!;

    /// <summary>
    /// The unique identifier of the account (optional).
    /// </summary>
    /// <example>1234567890</example>
    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    /// <summary>
    /// The initial balance of the account (in cents).
    /// </summary>
    /// <example>1000</example>
    [JsonPropertyName("initial_balance")]
    public long InitialBalance { get; init; }

    /// <summary>
    /// The credit limit of the account (in cents).
    /// </summary>
    /// <example>5000</example>
    [JsonPropertyName("credit_limit")]
    public long CreditLimit { get; init; }

    /// <summary>
    /// The currency of the account (default: BRL).
    /// </summary>
    /// <example>BRL</example>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "BRL";
}
