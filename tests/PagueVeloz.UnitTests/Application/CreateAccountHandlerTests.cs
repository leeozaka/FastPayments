using FluentAssertions;
using NSubstitute;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.UseCases.Accounts;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Interfaces.Repositories;
using Xunit;

namespace PagueVeloz.UnitTests.Application;

public class CreateAccountHandlerTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly IClientRepository _clientRepository = Substitute.For<IClientRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateAccountHandler _handler;

    public CreateAccountHandlerTests()
    {
        _handler = new CreateAccountHandler(_accountRepository, _clientRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_NewClient_ShouldCreateClientAndAccount()
    {
        _clientRepository.GetByClientIdAsync("CLI-NEW", Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-NEW", "ACC-NEW", 0, 0, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccountId.Should().Be("ACC-NEW");
        result.ClientId.Should().Be("CLI-NEW");
        await _clientRepository.Received(1).AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingClient_ShouldOnlyCreateAccount()
    {
        var existingClient = Client.Create("CLI-EXIST", "Existing Client");
        _clientRepository.GetByClientIdAsync("CLI-EXIST", Arg.Any<CancellationToken>())
            .Returns(existingClient);

        var command = new CreateAccountCommand("CLI-EXIST", "ACC-EXIST", 0, 0, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccountId.Should().Be("ACC-EXIST");
        await _clientRepository.DidNotReceive().AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInitialBalance_ShouldCreditAccount()
    {
        _clientRepository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-001", "ACC-BAL", 50_000, 0, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Balance.Should().Be(50_000);
        result.AvailableBalance.Should().Be(50_000);
    }

    [Fact]
    public async Task Handle_WithZeroInitialBalance_ShouldNotCredit()
    {
        _clientRepository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-001", "ACC-ZERO", 0, 0, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Balance.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNullAccountId_ShouldGenerateId()
    {
        _clientRepository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-001", null, 0, 0, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccountId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ShouldCallSaveChanges()
    {
        _clientRepository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-001", "ACC-SAVE", 0, 0, "BRL");

        await _handler.Handle(command, CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectResponse()
    {
        _clientRepository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        var command = new CreateAccountCommand("CLI-001", "ACC-RESP", 10_000, 50_000, "BRL");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeOfType<AccountResponse>();
        result.AccountId.Should().Be("ACC-RESP");
        result.ClientId.Should().Be("CLI-001");
        result.Balance.Should().Be(10_000);
        result.CreditLimit.Should().Be(50_000);
        result.Currency.Should().Be("BRL");
        result.Status.Should().Be("active");
    }
}
