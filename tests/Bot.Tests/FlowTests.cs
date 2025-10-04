using Bot;
using Bot.Services;
using Bot.Models;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace Bot.Tests;

public class FlowTests
{
    private (BotHandlers handlers, Mock<ITelegramBotClient> bot, GuestRepository repo) Make(string? adminHandle = null)
    {
        var bot = new Mock<ITelegramBotClient>(MockBehavior.Strict);
        // We only assert that messages are attempted; don't validate exact parameters everywhere
        bot.SetupAnySend();
        var repo = new GuestRepository(Path.Combine(Path.GetTempPath(), $"Repo_{Guid.NewGuid():N}.jsonl"));
        var handlers = new BotHandlers(bot.Object, repo, null, adminHandle);
        return (handlers, bot, repo);
    }

    [Fact]
    public async Task Language_Flow_InvalidThenValid()
    {
        var (h, bot, _) = Make();
        var s = new Session { ChatId = 1 };
        h.SetSession(1, s);
        await h.OnLanguage(1, s, "XX", default);
        await h.OnLanguage(1, s, "fi", default);
        Assert.Equal("fi", s.Language);
        bot.VerifyAnySend(Times.AtLeastOnce());
    }

    [Fact]
    public async Task Full_Signup_Without_Avec()
    {
        var (h, bot, repo) = Make();
        var s = new Session { ChatId = 1, UserId = 123 };
        h.SetSession(1, s);
        await h.OnLanguage(1, s, "en", default);
        await h.OnFullName(1, s, "Alice Wonderland", default);
        await h.OnPlusOne(1, s, "no", default);
        Assert.Equal(Step.Completed, s.Step);
        var latest = await repo.GetLatestForAsync(123, 1);
        Assert.NotNull(latest);
        Assert.Equal("Active", latest!.Status);
        Assert.Null(latest.AvecFullName);
        bot.VerifyAnySend(Times.AtLeastOnce());
    }

    [Fact]
    public async Task Avec_Cancel_During_Flow()
    {
        var (h, bot, repo) = Make();
        var s = new Session { ChatId = 2, UserId = 222, Language = "en" };
        await h.OnPlusOne(2, s, "yes", default);
        await h.OnAvecName(2, s, "none", default);
        var latest = await repo.GetLatestForAsync(222, 2);
        Assert.NotNull(latest);
        Assert.Null(latest!.AvecFullName);
        bot.VerifyAnySend(Times.AtLeastOnce());
    }

    [Fact]
    public async Task Change_Avec_After_Submit_And_Remove()
    {
        var (h, bot, repo) = Make();
        var s = new Session { ChatId = 3, UserId = 333, Language = "en", FullName = "Tester", Step = Step.Completed };
        await h.OnChangeAvecName(3, s, "Bob Friend", default);
        var latest = await repo.GetLatestForAsync(333, 3);
        Assert.Equal("Bob Friend", latest!.AvecFullName);
        await h.SignOut(3, s, default);
        latest = await repo.GetLatestForAsync(333, 3);
        Assert.Equal("Deleted", latest!.Status);
        bot.VerifyAnySend(Times.AtLeastOnce());
    }
}

internal static class MoqTelegramExtensions
{
    public static void SetupAnySend(this Mock<ITelegramBotClient> mock)
    {
        mock.Setup(m => m.SendTextMessageAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<Telegram.Bot.Types.Enums.ParseMode?>(),
                It.IsAny<IEnumerable<Telegram.Bot.Types.MessageEntity>>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<Telegram.Bot.Types.ReplyMarkups.IReplyMarkup>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((Telegram.Bot.Types.Message?)null!);

        mock.Setup(m => m.SendDocumentAsync(
                It.IsAny<long>(), It.IsAny<Telegram.Bot.Types.InputFile>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<Telegram.Bot.Types.MessageEntity>>(), It.IsAny<bool>(), It.IsAny<int>(),
                It.IsAny<Telegram.Bot.Types.ReplyMarkups.IReplyMarkup>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((Telegram.Bot.Types.Message?)null!);
    }

    public static void VerifyAnySend(this Mock<ITelegramBotClient> mock, Times times)
    {
        mock.Verify(m => m.SendTextMessageAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<Telegram.Bot.Types.Enums.ParseMode?>(),
            It.IsAny<IEnumerable<Telegram.Bot.Types.MessageEntity>>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<IReplyMarkup>(), It.IsAny<System.Threading.CancellationToken>()), times);
    }
}
