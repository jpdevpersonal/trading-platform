namespace TradingAssistant.Api;

public static class LocalOrigin
{
    public static string Parse(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !uri.IsLoopback
            || !string.Equals(value, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal)
            || uri.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("FrontendOrigin must be an explicit loopback HTTP(S) origin without a trailing slash.");
        }

        return value;
    }
}
