using Microsoft.EntityFrameworkCore;

namespace TradingAssistant.Infrastructure;

public sealed class TradingContext(DbContextOptions<TradingContext> options) : DbContext(options);
