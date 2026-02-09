namespace PagueVeloz.Application.Interfaces;

public interface IMetricsService
{
    void RecordTransactionProcessed(string operation, string status);
    void RecordTransactionDuration(string operation, double durationMs);
    void RecordTransactionError(string operation, string errorType);
}
