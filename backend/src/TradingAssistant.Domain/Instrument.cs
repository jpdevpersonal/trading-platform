namespace TradingAssistant.Domain;

public sealed class Instrument
{
    public Instrument(Guid id, string symbol, string exchangeMic, string name, string currency, bool isSample)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An instrument identifier must not be empty.", nameof(id));
        }
        if (string.IsNullOrEmpty(symbol) || symbol.Length > 32 || !IsLetterOrDigit(symbol[0])
            || symbol.Any(character => !IsLetterOrDigit(character) && character is not '.' and not '-'))
        {
            throw new ArgumentException("A symbol must contain 1-32 uppercase ASCII letters, digits, dots or hyphens and start with a letter or digit.", nameof(symbol));
        }
        if (exchangeMic is not { Length: 4 } || exchangeMic.Any(character => !IsLetterOrDigit(character)))
        {
            throw new ArgumentException("An exchange MIC must contain four uppercase ASCII letters or digits.", nameof(exchangeMic));
        }
        ArgumentNullException.ThrowIfNull(name);
        name = name.Trim();
        if (name.Length is < 1 or > 200)
        {
            throw new ArgumentException("A name must contain 1-200 nonblank characters after trimming.", nameof(name));
        }
        if (currency is not { Length: 3 } || currency.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("A currency must contain three uppercase ASCII letters.", nameof(currency));
        }

        Id = id;
        Symbol = symbol;
        ExchangeMic = exchangeMic;
        Name = name;
        Currency = currency;
        IsSample = isSample;
    }

    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public string ExchangeMic { get; private set; }
    public string Name { get; private set; }
    public string Currency { get; private set; }
    public bool IsSample { get; private set; }

    private static bool IsLetterOrDigit(char character) => character is >= 'A' and <= 'Z' or >= '0' and <= '9';
}
