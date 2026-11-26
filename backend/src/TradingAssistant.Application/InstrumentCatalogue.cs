using TradingAssistant.Domain;

namespace TradingAssistant.Application;

public sealed class InstrumentCatalogue(IInstrumentReader reader)
{
    public Task<IReadOnlyList<Instrument>> ListAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        return reader.ListAsync(offset, limit, cancellationToken);
    }

    public Task<Instrument?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        reader.FindAsync(id, cancellationToken);
}
