namespace PagueVeloz.Application.Interfaces;

public interface ITraceable
{
    string ReferenceId { get; }
    string AccountId { get; }
    string Operation { get; }
}
