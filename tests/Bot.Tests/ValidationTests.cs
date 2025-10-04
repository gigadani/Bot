using Bot;
using Xunit;

namespace Bot.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("fi", true)]
    [InlineData("en", true)]
    [InlineData("FI", true)]
    [InlineData("xx", false)]
    public void TryNormalizeLanguage_Works(string input, bool ok)
    {
        Assert.Equal(ok, BotHandlers.TryNormalizeLanguage(input, out var lang));
        if (ok) Assert.True(lang == "fi" || lang == "en");
    }

    [Theory]
    [InlineData("John Doe", true)]
    [InlineData("j d", false)]
    [InlineData("J", false)]
    [InlineData("Anna-Maria O'Neil", true)]
    [InlineData("John 123", false)]
    public void LooksLikeRealName_Works(string name, bool ok)
    {
        Assert.Equal(ok, BotHandlers.LooksLikeRealName(name));
    }

    [Fact]
    public void NormalizeName_TitleCases()
    {
        Assert.Equal("John Doe", BotHandlers.NormalizeName("john DOE"));
        Assert.Equal("Anna-Maria O'neil", BotHandlers.NormalizeName("ANNA-mARIA O'NEIL"));
    }

    [Theory]
    [InlineData("en", "yes", true, true)]
    [InlineData("en", "no", true, false)]
    [InlineData("fi", "kyllä", true, true)]
    [InlineData("fi", "ei", true, false)]
    [InlineData("en", "maybe", false, false)]
    public void ParseYesNo_Works(string lang, string input, bool ok, bool expected)
    {
        var res = BotHandlers.ParseYesNo(lang, input, out var yes);
        Assert.Equal(ok, res);
        if (ok) Assert.Equal(expected, yes);
    }

    [Theory]
    [InlineData("en", "none", true)]
    [InlineData("fi", "ei", true)]
    [InlineData("en", "remove", true)]
    [InlineData("en", "keep", false)]
    public void IsCancelAvec_Works(string lang, string input, bool expected)
    {
        Assert.Equal(expected, BotHandlers.IsCancelAvec(lang, input));
    }
}

