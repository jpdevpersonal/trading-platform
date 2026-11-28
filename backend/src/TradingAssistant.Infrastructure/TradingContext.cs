using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain;

namespace TradingAssistant.Infrastructure;

public sealed class TradingContext(DbContextOptions<TradingContext> options) : DbContext(options)
{
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<DailyPriceRow> DailyPrices => Set<DailyPriceRow>();
    public DbSet<DailyPriceRefresh> DailyPriceRefreshes => Set<DailyPriceRefresh>();

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

        var bar = modelBuilder.Entity<DailyPriceRow>();
        bar.HasKey(value => new { value.InstrumentId, value.TradingDate });
        bar.HasOne<Instrument>().WithMany().HasForeignKey(value => value.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in new[] { "Open", "High", "Low", "Close", "Volume" })
            bar.Property<decimal>(property).HasPrecision(28, 10);
        bar.Property(value => value.Source).HasMaxLength(32).IsRequired();
        bar.Property(value => value.PriceBasis).HasMaxLength(16).IsRequired();
        bar.ToTable(table => table.HasCheckConstraint("CK_DailyPrices_Values",
            "\"Low\" > 0 AND \"Open\" >= \"Low\" AND \"Close\" >= \"Low\" AND \"High\" >= \"Open\" AND \"High\" >= \"Close\" AND \"Volume\" >= 0"));

        var refresh = modelBuilder.Entity<DailyPriceRefresh>();
        refresh.HasKey(value => value.InstrumentId);
        refresh.Property(value => value.InstrumentId).ValueGeneratedNever();
        refresh.HasOne<Instrument>().WithMany().HasForeignKey(value => value.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        refresh.Property(value => value.Source).HasMaxLength(32).IsRequired();
        refresh.Property(value => value.PriceBasis).HasMaxLength(16).IsRequired();
    }
}
