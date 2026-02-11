using Ardalis.Result;
using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Mappers;
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
    IDistributedLockService lockService,
    ICacheService cacheService,
    IMetricsService metricsService) : IRequestHandler<ProcessTransactionCommand, Result<TransactionResponse>>
{
    private static readonly TimeSpan IdempotencyCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<TransactionResponse>> Handle(ProcessTransactionCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var idempotencyKey = CacheKeys.Idempotency(request.ReferenceId);
        var cachedResponse = await cacheService.GetAsync<TransactionResponse>(idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (cachedResponse is not null)
        {
            return Result.Success(cachedResponse);
        }

        // Slow path: check database
        var existingTransaction = await transactionRepository
            .GetByReferenceIdAsync(request.ReferenceId, cancellationToken)
            .ConfigureAwait(false);

        if (existingTransaction is not null)
        {
            var existingAccount = await accountRepository
                .GetByAccountIdReadOnlyAsync(request.AccountId, cancellationToken)
                .ConfigureAwait(false);

            var response = existingTransaction.ToResponse(existingAccount);

            await cacheService.SetAsync(idempotencyKey, response, IdempotencyCacheTtl, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(response);
        }

        var operationResult = ParseOperation(request.Operation);
        if (!operationResult.IsSuccess)
            return Result.Invalid(operationResult.ValidationErrors.ToArray());

        var operation = operationResult.Value;

        if (operation == TransactionType.Transfer)
        {
            var transferResult = await ProcessTransferViaSagaAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!transferResult.IsSuccess)
            {
                metricsService.RecordTransactionDuration(request.Operation, stopwatch.Elapsed.TotalMilliseconds);
                metricsService.RecordTransactionProcessed(request.Operation, "failed");
                metricsService.RecordTransactionError(request.Operation, "transfer_failed");
                return transferResult;
            }

            var transferResponse = transferResult.Value;
            metricsService.RecordTransactionDuration(request.Operation, stopwatch.Elapsed.TotalMilliseconds);
            metricsService.RecordTransactionProcessed(request.Operation, transferResponse.Status);

            // Cache the transfer result
            await cacheService.SetAsync(idempotencyKey, transferResponse, IdempotencyCacheTtl, cancellationToken)
                .ConfigureAwait(false);

            return transferResult;
        }

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

        await InvalidateBalanceCacheAsync(request.AccountId, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        var status = transaction.Status.ToString().ToLowerInvariant();
        metricsService.RecordTransactionDuration(request.Operation, stopwatch.Elapsed.TotalMilliseconds);
        metricsService.RecordTransactionProcessed(request.Operation, status);

        var result = transaction.ToResponse(account);

        if (transaction.Status == TransactionStatus.Failed)
        {
            metricsService.RecordTransactionError(request.Operation, "transaction_failed");
            return Result.Error(transaction.ErrorMessage ?? "Transaction failed.");
        }

        await cacheService.SetAsync(idempotencyKey, result, IdempotencyCacheTtl, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(result);
    }

    private async Task InvalidateBalanceCacheAsync(string accountId, CancellationToken cancellationToken)
    {
        var balanceCacheKey = CacheKeys.AccountBalance(accountId);
        await cacheService.RemoveAsync(balanceCacheKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TransactionResponse>> ProcessTransferViaSagaAsync(ProcessTransactionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationAccountId))
            return Result.Invalid(new ValidationError("Transfer requires 'destination_account_id'."));

        if (string.Equals(request.AccountId, request.DestinationAccountId, StringComparison.OrdinalIgnoreCase))
            return Result.Error("Source and destination accounts cannot be the same.");

        var sagaResult = await transferSagaService.ExecuteTransferAsync(
            request.AccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency,
            request.ReferenceId,
            request.Metadata,
            ct).ConfigureAwait(false);

        await InvalidateBalanceCacheAsync(request.AccountId, ct).ConfigureAwait(false);
        await InvalidateBalanceCacheAsync(request.DestinationAccountId, ct).ConfigureAwait(false);

        var response = sagaResult.ToResponse();

        return !sagaResult.Success
            ? Result.Error(response.ErrorMessage ?? "Transfer failed.")
            : Result.Success(response);
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

    private static Result<TransactionType> ParseOperation(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "credit" => Result.Success(TransactionType.Credit),
            "debit" => Result.Success(TransactionType.Debit),
            "reserve" => Result.Success(TransactionType.Reserve),
            "capture" => Result.Success(TransactionType.Capture),
            "reversal" => Result.Success(TransactionType.Reversal),
            "transfer" => Result.Success(TransactionType.Transfer),
            _ => Result.Invalid(new ValidationError($"Unknown operation: {operation}"))
        };
    }
}
