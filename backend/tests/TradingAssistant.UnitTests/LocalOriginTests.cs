using TradingAssistant.Api;
using Xunit;

namespace TradingAssistant.UnitTests;

public class LocalOriginTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    [InlineData("https://example.com")]
    [InlineData("http://localhost.example.com")]
    [InlineData("http://localhost:3000/path")]
    [InlineData("http://user@localhost:3000")]
    [InlineData("http://localhost:3000?token=test")]
    public void RejectsValuesThatAreNotSingleLocalOrigins(string? origin)
    {
        Assert.Throws<InvalidOperationException>(() => LocalOrigin.Parse(origin));
    }

    [Theory]
    [InlineData("https://localhost:3443")]
    [InlineData("http://127.0.0.1:3001")]
    public void AllowsExplicitLoopbackOverrides(string origin)
    {
        Assert.Equal(origin, LocalOrigin.Parse(origin));
    }
}
