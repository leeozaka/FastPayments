using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Domain.Interfaces.Services;

namespace PagueVeloz.Application.UseCases.Transactions;

public sealed class ProcessTransactionHandler(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ITransferService transferService,
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
                .GetByAccountIdAsync(request.AccountId, cancellationToken)
                .ConfigureAwait(false);

            return MapToResponse(existingTransaction, existingAccount);
        }

        await using var lockHandle = await lockService
            .AcquireLockAsync($"account:{request.AccountId}", TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);

        var operation = ParseOperation(request.Operation);

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var (transaction, account) = operation switch
            {
                TransactionType.Credit => await ProcessCreditAsync(request, cancellationToken).ConfigureAwait(false),
                TransactionType.Debit => await ProcessDebitAsync(request, cancellationToken).ConfigureAwait(false),
                TransactionType.Reserve => await ProcessReserveAsync(request, cancellationToken).ConfigureAwait(false),
                TransactionType.Capture => await ProcessCaptureAsync(request, cancellationToken).ConfigureAwait(false),
                TransactionType.Reversal => await ProcessReversalAsync(request, cancellationToken).ConfigureAwait(false),
                TransactionType.Transfer => await ProcessTransferAsync(request, cancellationToken).ConfigureAwait(false),
                _ => throw new DomainException("INVALID_OPERATION", $"Unsupported operation: {request.Operation}")
            };

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAllAsync(account.DomainEvents, cancellationToken).ConfigureAwait(false);
            account.ClearDomainEvents();

            return MapToResponse(transaction, account);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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

        if (originalTransaction.Status != TransactionStatus.Success)
            throw new DomainException("INVALID_REVERSAL", "Can only reverse successful transactions.");

        Transaction transaction;
        switch (originalTransaction.Type)
        {
            case TransactionType.Credit:
                transaction = account.Debit(originalTransaction.Amount, request.Currency, request.ReferenceId, request.Metadata);
                break;
            case TransactionType.Debit:
                transaction = account.Credit(originalTransaction.Amount, request.Currency, request.ReferenceId, request.Metadata);
                break;
            default:
                throw new DomainException("INVALID_REVERSAL", $"Cannot reverse operation of type {originalTransaction.Type}.");
        }

        originalTransaction.MarkAsReversed();
        await accountRepository.UpdateAsync(account, ct).ConfigureAwait(false);
        await transactionRepository.AddAsync(transaction, ct).ConfigureAwait(false);
        return (transaction, account);
    }

    private async Task<(Transaction, Account)> ProcessTransferAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationAccountId))
            throw new DomainException("MISSING_DESTINATION", "Transfer requires 'destination_account_id'.");

        var (debitTx, _) = await transferService
            .TransferAsync(request.AccountId, request.DestinationAccountId,
                request.Amount, request.Currency, request.ReferenceId, request.Metadata, ct)
            .ConfigureAwait(false);

        var sourceAccount = await GetAccountOrThrowAsync(request.AccountId, ct).ConfigureAwait(false);
        return (debitTx, sourceAccount);
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
