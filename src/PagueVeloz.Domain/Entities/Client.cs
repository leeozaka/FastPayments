namespace PagueVeloz.Domain.Entities;

public sealed class Client : Entity
{
    public string ClientId { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private readonly List<Account> _accounts = [];
    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

    private Client() { }

    public static Client Create(string clientId, string name)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID is required.", nameof(clientId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new Client
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Account AddAccount(string accountId, long creditLimit, string currency)
    {
        var account = Account.Create(accountId, ClientId, creditLimit, currency);
        _accounts.Add(account);
        return account;
    }
}
