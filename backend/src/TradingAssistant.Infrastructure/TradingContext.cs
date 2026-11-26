using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain;

namespace TradingAssistant.Infrastructure;

public sealed class TradingContext(DbContextOptions<TradingContext> options) : DbContext(options)
{
    public DbSet<Instrument> Instruments => Set<Instrument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var instrument = modelBuilder.Entity<Instrument>();
        instrument.HasKey(value => value.Id);
        instrument.Property(value => value.Id).ValueGeneratedNever();
        instrument.Property(value => value.Symbol).HasMaxLength(32).IsRequired();
        instrument.Property(value => value.ExchangeMic).HasMaxLength(4).IsRequired();
        instrument.Property(value => value.Name).HasMaxLength(200).IsRequired();
        instrument.Property(value => value.Currency).HasMaxLength(3).IsRequired();
        instrument.Property(value => value.IsSample).IsRequired();
        instrument.HasIndex(value => new { value.ExchangeMic, value.Symbol }).IsUnique();
    }
}
