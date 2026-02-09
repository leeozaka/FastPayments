using System.Diagnostics.Metrics;
using PagueVeloz.Application.Interfaces;

namespace PagueVeloz.Infrastructure.Metrics;

public sealed class MetricsService : IMetricsService
{
    public const string MeterName = "PagueVeloz.Transactions";

    private readonly Counter<long> _transactionsTotal;
    private readonly Histogram<double> _transactionDuration;
    private readonly Counter<long> _transactionErrors;

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _transactionsTotal = meter.CreateCounter<long>(
            "transactions_total",
            unit: "{transaction}",
            description: "Total transactions processed");

        _transactionDuration = meter.CreateHistogram<double>(
            "transaction_duration_milliseconds",
            unit: "ms",
            description: "Transaction processing duration");

        _transactionErrors = meter.CreateCounter<long>(
            "transactions_errors_total",
            unit: "{error}",
            description: "Total transaction errors");
    }

    public void RecordTransactionProcessed(string operation, string status)
    {
        _transactionsTotal.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", status));
    }

    public void RecordTransactionDuration(string operation, double durationMs)
    {
        _transactionDuration.Record(durationMs,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordTransactionError(string operation, string errorType)
    {
        _transactionErrors.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
}
