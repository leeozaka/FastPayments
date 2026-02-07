using FluentValidation;
using PagueVeloz.Application.UseCases.Transactions;

namespace PagueVeloz.Application.Validators;

public sealed class ProcessTransactionValidator : AbstractValidator<ProcessTransactionCommand>
{
    private static readonly HashSet<string> ValidOperations =
        ["credit", "debit", "reserve", "capture", "reversal", "transfer"];

    public ProcessTransactionValidator()
    {
        RuleFor(x => x.Operation)
            .NotEmpty().WithMessage("Operation is required.")
            .Must(op => ValidOperations.Contains(op.ToLowerInvariant()))
            .WithMessage("Invalid operation. Must be one of: credit, debit, reserve, capture, reversal, transfer.");

        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be a 3-letter code.");

        RuleFor(x => x.ReferenceId)
            .NotEmpty().WithMessage("Reference ID is required.");

        RuleFor(x => x.DestinationAccountId)
            .NotEmpty()
            .When(x => x.Operation.Equals("transfer", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Destination account ID is required for transfers.");
    }
}
