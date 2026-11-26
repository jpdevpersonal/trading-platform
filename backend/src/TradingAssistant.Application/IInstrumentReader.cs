using TradingAssistant.Domain;

namespace TradingAssistant.Application;

public interface IInstrumentReader
{
    Task<IReadOnlyList<Instrument>> ListAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<Instrument?> FindAsync(Guid id, CancellationToken cancellationToken);
}
