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

    [Fact]
    public void Metadata_WithTooManyKeys_ShouldFail()
    {
        var metadata = Enumerable.Range(1, 11)
            .ToDictionary(i => $"key{i}", i => $"value{i}");

        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 10000, "BRL", "TXN-001", null, metadata);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("10 keys"));
    }

    [Fact]
    public void Metadata_WithTooLongKey_ShouldFail()
    {
        var metadata = new Dictionary<string, string>
        {
            { new string('x', 65), "value" }
        };

        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 10000, "BRL", "TXN-001", null, metadata);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("64 characters"));
    }

    [Fact]
    public void Metadata_WithTooLongValue_ShouldFail()
    {
        var metadata = new Dictionary<string, string>
        {
            { "key", new string('x', 257) }
        };

        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 10000, "BRL", "TXN-001", null, metadata);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("256 characters"));
    }

    [Fact]
    public void Metadata_WithValidData_ShouldPass()
    {
        var metadata = new Dictionary<string, string>
        {
            { "description", "Test transaction" },
            { "orderId", "ORD-12345" }
        };

        var command = new ProcessTransactionCommand(
            "credit", "ACC-001", 10000, "BRL", "TXN-001", null, metadata);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Transfer_SameAccountAsDestination_ShouldFail()
    {
        var command = new ProcessTransactionCommand(
            "transfer", "ACC-001", 10000, "BRL", "TXN-001", "ACC-001", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("same"));
    }

    [Fact]
    public void Transfer_DifferentDestination_ShouldPass()
    {
        var command = new ProcessTransactionCommand(
            "transfer", "ACC-001", 10000, "BRL", "TXN-001", "ACC-002", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
