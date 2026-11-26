using TradingAssistant.Domain;
using Xunit;

namespace TradingAssistant.UnitTests;

public class InstrumentTests
{
    [Theory]
    [InlineData("S-AO", "XAAA", "  Alder Orbit  ", "USD", true, "Alder Orbit")]
    [InlineData("1.A-Z", "X1A2", "Example", "EUR", false, "Example")]
    public void CreatesValidInstrument(string symbol, string mic, string name, string currency, bool isSample, string expectedName)
    {
        var id = Guid.NewGuid();
        var instrument = new Instrument(id, symbol, mic, name, currency, isSample);
        Assert.Equal(id, instrument.Id);
        Assert.Equal(symbol, instrument.Symbol);
        Assert.Equal(mic, instrument.ExchangeMic);
        Assert.Equal(expectedName, instrument.Name);
        Assert.Equal(currency, instrument.Currency);
        Assert.Equal(isSample, instrument.IsSample);
    }

    [Fact]
    public void AcceptsMaximumLengthsAndRejectsExcess()
    {
        _ = new Instrument(Guid.NewGuid(), new string('A', 32), "XAAA", new string('N', 200), "USD", false);
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.NewGuid(), new string('A', 33), "XAAA", "Name", "USD", false));
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.NewGuid(), "A", "XAAA", new string('N', 201), "USD", false));
    }

    [Fact]
    public void RejectsEmptyIdentifier() =>
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.Empty, "A", "XAAA", "Name", "USD", false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".A")]
    [InlineData("-A")]
    [InlineData("a")]
    [InlineData(" A")]
    [InlineData("A ")]
    [InlineData("A/B")]
    [InlineData("A_B")]
    [InlineData("\u00c9")]
    [InlineData("A\n")]
    public void RejectsInvalidSymbol(string? symbol) =>
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.NewGuid(), symbol!, "XAAA", "Name", "USD", false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XAA")]
    [InlineData("XAAAA")]
    [InlineData("xaaa")]
    [InlineData("XA-A")]
    [InlineData("XAA ")]
    [InlineData("XA\u00c9A")]
    public void RejectsInvalidMic(string? mic) =>
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.NewGuid(), "A", mic!, "Name", "USD", false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void RejectsBlankName(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => new Instrument(Guid.NewGuid(), "A", "XAAA", name!, "USD", false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("usd")]
    [InlineData("US1")]
    [InlineData("US ")]
    [InlineData("U\u00c9D")]
    public void RejectsInvalidCurrency(string? currency) =>
        Assert.Throws<ArgumentException>(() => new Instrument(Guid.NewGuid(), "A", "XAAA", "Name", currency!, false));
}
