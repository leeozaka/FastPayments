using FluentAssertions;
using PagueVeloz.Application.UseCases.Transactions;
using PagueVeloz.Application.Validators;
using Xunit;

namespace PagueVeloz.UnitTests.Application;

public class ProcessTransactionValidatorTests
{
    private readonly ProcessTransactionValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 10000, "BRL", "TXN-001", null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyOperation_ShouldFail()
    {
        var command = new ProcessTransactionCommand(
            "", "ACC-001", 10000, "BRL", "TXN-001", null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidOperation_ShouldFail()
    {
        var command = new ProcessTransactionCommand(
            "invalid_op", "ACC-001", 10000, "BRL", "TXN-001", null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ZeroAmount_ShouldFail()
    {
        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 0, "BRL", "TXN-001", null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Transfer_WithoutDestination_ShouldFail()
    {
        var command = new ProcessTransactionCommand(
            "transfer", "ACC-001", 10000, "BRL", "TXN-001", null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Transfer_WithDestination_ShouldPass()
    {
        var command = new ProcessTransactionCommand(
            "transfer", "ACC-001", 10000, "BRL", "TXN-001", "ACC-002", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
