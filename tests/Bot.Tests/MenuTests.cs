using Bot;
using Xunit;

namespace Bot.Tests;

public class MenuTests
{
    [Fact]
    public void Labels_Localize()
    {
        var en = BotHandlers.CompletedMenuLabels("en");
        var fi = BotHandlers.CompletedMenuLabels("fi");
        Assert.Equal("Change +1 name", en.changeAvec);
        Assert.Equal("Remove signup", en.removeSignup);
        Assert.Equal("Vaihda avecin nimi", fi.changeAvec);
        Assert.Equal("Peru ilmoittautuminen", fi.removeSignup);
    }

    [Fact]
    public void Command_Matchers_Work()
    {
        Assert.True(BotHandlers.IsChangeAvecCommand("en", "Change +1 name"));
        Assert.True(BotHandlers.IsChangeAvecCommand("en", "/avec"));
        Assert.True(BotHandlers.IsChangeAvecCommand("fi", "Vaihda avecin nimi"));

        Assert.True(BotHandlers.IsRemoveSignupCommand("en", "Remove signup"));
        Assert.True(BotHandlers.IsRemoveSignupCommand("en", "/removeme"));
        Assert.True(BotHandlers.IsRemoveSignupCommand("fi", "Peru ilmoittautuminen"));

        Assert.Equal("Export CSV", BotHandlers.ExportLabel("en"));
        Assert.Equal("Vie CSV", BotHandlers.ExportLabel("fi"));
        Assert.Equal("Broadcast", BotHandlers.BroadcastLabel("en"));
        Assert.Equal("Lähetä kaikille", BotHandlers.BroadcastLabel("fi"));

        Assert.True(BotHandlers.IsExportCommand("en", "Export CSV"));
        Assert.True(BotHandlers.IsExportCommand("fi", "Vie CSV"));
        Assert.True(BotHandlers.IsExportCommand("en", "/export"));
        Assert.True(BotHandlers.IsBroadcastCommand("en", "Broadcast"));
        Assert.True(BotHandlers.IsBroadcastCommand("fi", "Lähetä kaikille"));
        Assert.True(BotHandlers.IsBroadcastCommand("en", "/broadcast"));
    }
}
