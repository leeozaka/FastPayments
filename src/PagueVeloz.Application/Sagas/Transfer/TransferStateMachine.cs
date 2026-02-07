using MassTransit;

namespace PagueVeloz.Application.Sagas.Transfer;

public sealed class TransferStateMachine : MassTransitStateMachine<TransferSagaState>
{
    public State DebitingSource { get; private set; } = null!;
    public State CreditingDestination { get; private set; } = null!;
    public State CompensatingDebit { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    public Event<TransferRequested> TransferRequested { get; private set; } = null!;
    public Event<DebitSourceCompleted> DebitSourceCompleted { get; private set; } = null!;
    public Event<DebitSourceFailed> DebitSourceFailed { get; private set; } = null!;
    public Event<CreditDestinationCompleted> CreditDestinationCompleted { get; private set; } = null!;
    public Event<CreditDestinationFailed> CreditDestinationFailed { get; private set; } = null!;
    public Event<CompensateDebitCompleted> CompensateDebitCompleted { get; private set; } = null!;

    public TransferStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => TransferRequested, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => DebitSourceCompleted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => DebitSourceFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CreditDestinationCompleted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CreditDestinationFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CompensateDebitCompleted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Initially(
            When(TransferRequested)
                .Then(ctx =>
                {
                    ctx.Saga.SourceAccountId = ctx.Message.SourceAccountId;
                    ctx.Saga.DestinationAccountId = ctx.Message.DestinationAccountId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.ReferenceId = ctx.Message.ReferenceId;
                    ctx.Saga.Metadata = ctx.Message.Metadata;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<DebitSourceCommand>(new
                {
                    ctx.Saga.CorrelationId,
                    AccountId = ctx.Saga.SourceAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Currency,
                    ReferenceId = $"{ctx.Saga.ReferenceId}-DEBIT",
                    ctx.Saga.Metadata
                }))
                .TransitionTo(DebitingSource)
        );

        During(DebitingSource,
            When(DebitSourceCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.DebitTransactionId = ctx.Message.TransactionId;
                    ctx.Saga.SourceBalance = ctx.Message.SourceBalance;
                    ctx.Saga.SourceReservedBalance = ctx.Message.SourceReservedBalance;
                    ctx.Saga.SourceAvailableBalance = ctx.Message.SourceAvailableBalance;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<CreditDestinationCommand>(new
                {
                    ctx.Saga.CorrelationId,
                    AccountId = ctx.Saga.DestinationAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Currency,
                    ReferenceId = $"{ctx.Saga.ReferenceId}-CREDIT",
                    ctx.Saga.Metadata
                }))
                .TransitionTo(CreditingDestination),

            When(DebitSourceFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<TransferFailed>(new
                {
                    ctx.Saga.CorrelationId,
                    ctx.Saga.SourceAccountId,
                    ctx.Saga.DestinationAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.ReferenceId,
                    Reason = ctx.Saga.FailureReason!
                }))
                .TransitionTo(Faulted)
                .Finalize()
        );

        During(CreditingDestination,
            When(CreditDestinationCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.CreditTransactionId = ctx.Message.TransactionId;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<TransferCompleted>(new
                {
                    ctx.Saga.CorrelationId,
                    ctx.Saga.SourceAccountId,
                    ctx.Saga.DestinationAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Currency,
                    ctx.Saga.ReferenceId,
                    DebitTransactionId = ctx.Saga.DebitTransactionId!,
                    CreditTransactionId = ctx.Saga.CreditTransactionId!,
                    ctx.Saga.SourceBalance,
                    ctx.Saga.SourceReservedBalance,
                    ctx.Saga.SourceAvailableBalance
                }))
                .TransitionTo(Completed)
                .Finalize(),

            When(CreditDestinationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<CompensateDebitCommand>(new
                {
                    ctx.Saga.CorrelationId,
                    AccountId = ctx.Saga.SourceAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Currency,
                    ReferenceId = $"{ctx.Saga.ReferenceId}-COMPENSATE"
                }))
                .TransitionTo(CompensatingDebit)
        );

        During(CompensatingDebit,
            When(CompensateDebitCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.SourceBalance = ctx.Message.SourceBalance;
                    ctx.Saga.SourceReservedBalance = ctx.Message.SourceReservedBalance;
                    ctx.Saga.SourceAvailableBalance = ctx.Message.SourceAvailableBalance;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<TransferFailed>(new
                {
                    ctx.Saga.CorrelationId,
                    ctx.Saga.SourceAccountId,
                    ctx.Saga.DestinationAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.ReferenceId,
                    Reason = $"Transfer compensated: {ctx.Saga.FailureReason}"
                }))
                .TransitionTo(Faulted)
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
