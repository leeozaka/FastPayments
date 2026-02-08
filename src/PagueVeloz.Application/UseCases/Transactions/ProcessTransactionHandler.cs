using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Application.UseCases.Transactions;

public sealed class ProcessTransactionHandler(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ITransferSagaService transferSagaService,
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    IDistributedLockService lockService) : IRequestHandler<ProcessTransactionCommand, TransactionResponse>
{
    public async Task<TransactionResponse> Handle(ProcessTransactionCommand request, CancellationToken cancellationToken)
    {
        var existingTransaction = await transactionRepository
            .GetByReferenceIdAsync(request.ReferenceId, cancellationToken)
            .ConfigureAwait(false);

        if (existingTransaction is not null)
        {
            var existingAccount = await accountRepository
                .GetByAccountIdReadOnlyAsync(request.AccountId, cancellationToken)
                .ConfigureAwait(false);

            return MapToResponse(existingTransaction, existingAccount);
        }

        var operation = ParseOperation(request.Operation);

        if (operation == TransactionType.Transfer)
            return await ProcessTransferViaSagaAsync(request, cancellationToken).ConfigureAwait(false);

        await using var lockHandle = await lockService
            .AcquireLockAsync($"account:{request.AccountId}", TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);

        var (transaction, account) = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            return operation switch
            {
                TransactionType.Credit => await ProcessCreditAsync(request, ct).ConfigureAwait(false),
                TransactionType.Debit => await ProcessDebitAsync(request, ct).ConfigureAwait(false),
                TransactionType.Reserve => await ProcessReserveAsync(request, ct).ConfigureAwait(false),
                TransactionType.Capture => await ProcessCaptureAsync(request, ct).ConfigureAwait(false),
                TransactionType.Reversal => await ProcessReversalAsync(request, ct).ConfigureAwait(false),
                _ => throw new DomainException("INVALID_OPERATION", $"Unsupported operation: {request.Operation}")
            };
        }, cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAllAsync(account.DomainEvents, cancellationToken).ConfigureAwait(false);
        account.ClearDomainEvents();

        return MapToResponse(transaction, account);
    }

    private async Task<TransactionResponse> ProcessTransferViaSagaAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationAccountId))
            throw new DomainException("MISSING_DESTINATION", "Transfer requires 'destination_account_id'.");

        var result = await transferSagaService.ExecuteTransferAsync(
            request.AccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency,
            request.ReferenceId,
            request.Metadata,
            ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return new TransactionResponse
            {
                TransactionId = $"{request.ReferenceId}-PROCESSED",
                Status = "failed",
                Balance = result.SourceBalance,
                ReservedBalance = result.SourceReservedBalance,
                AvailableBalance = result.SourceAvailableBalance,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = result.FailureReason
            };
        }

        return new TransactionResponse
        {
            TransactionId = $"{result.DebitTransactionId}-PROCESSED",
            Status = "success",
            Balance = result.SourceBalance,
            ReservedBalance = result.SourceReservedBalance,
            AvailableBalance = result.SourceAvailableBalance,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<(Transaction, Account)> ProcessCreditAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        var account = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);
        var transaction = account.Credit(request.Amount, request.Currency, request.ReferenceId, request.Metadata);
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<(Transaction, Account)> ProcessDebitAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        var account = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);
        var transaction = account.Debit(request.Amount, request.Currency, request.ReferenceId, request.Metadata);
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<(Transaction, Account)> ProcessReserveAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        var account = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);
        var transaction = account.Reserve(request.Amount, request.Currency, request.ReferenceId, request.Metadata);
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<(Transaction, Account)> ProcessCaptureAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        var account = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);
        var transaction = account.Capture(request.Amount, request.Currency, request.ReferenceId, request.Metadata);
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<(Transaction, Account)> ProcessReversalAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        var account = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);

        var originalRefId = request.Metadata?.GetValueOrDefault("original_reference_id");
        if (string.IsNullOrWhiteSpace(originalRefId))
            throw new DomainException("MISSING_ORIGINAL_REF", "Reversal requires 'original_reference_id' in metadata.");

        var originalTransaction = await transactionRepository
            .GetByReferenceIdAsync(originalRefId, ct)
            .ConfigureAwait(false)
            ?? throw new TransactionNotFoundException(originalRefId);

        var transaction = account.Reverse(originalTransaction, request.ReferenceId, request.Metadata);

        originalTransaction.MarkAsReversed();
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<Account> GetAccountOrThrowAsync(string accountId, CancellationToken ct)
    {
        return await accountRepository.GetByAccountIdAsync(accountId, ct).ConfigureAwait(false)
            ?? throw new DomainException("ACCOUNT_NOT_FOUND", $"Account '{accountId}' not found.");
    }

    private static TransactionType ParseOperation(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "credit" => TransactionType.Credit,
            "debit" => TransactionType.Debit,
            "reserve" => TransactionType.Reserve,
            "capture" => TransactionType.Capture,
            "reversal" => TransactionType.Reversal,
            "transfer" => TransactionType.Transfer,
            _ => throw new DomainException("INVALID_OPERATION", $"Unknown operation: {operation}")
        };
    }

    private static TransactionResponse MapToResponse(Transaction transaction, Account? account)
    {
        return new TransactionResponse
        {
            TransactionId = $"{transaction.ReferenceId}-PROCESSED",
            Status = transaction.Status.ToString().ToLowerInvariant(),
            Balance = account?.Balance ?? 0,
            ReservedBalance = account?.ReservedBalance ?? 0,
            AvailableBalance = account?.AvailableBalance ?? 0,
            Timestamp = transaction.Timestamp,
            ErrorMessage = transaction.ErrorMessage
        };
    }
}
