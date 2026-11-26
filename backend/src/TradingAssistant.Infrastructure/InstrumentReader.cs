using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application;
using TradingAssistant.Domain;

namespace TradingAssistant.Infrastructure;

internal sealed class InstrumentReader(TradingContext context) : IInstrumentReader
{
    public async Task<IReadOnlyList<Instrument>> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
        await context.Instruments.AsNoTracking()
            .OrderBy(instrument => instrument.ExchangeMic).ThenBy(instrument => instrument.Symbol)
            .Skip(offset).Take(limit).ToListAsync(cancellationToken);

    public Task<Instrument?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Instruments.AsNoTracking().SingleOrDefaultAsync(instrument => instrument.Id == id, cancellationToken);
}
