namespace TradingAssistant.Application;

public interface IDatabaseReadiness
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
