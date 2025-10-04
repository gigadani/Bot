using Bot.Services;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot;

public sealed partial class BotHandlers
{
    internal async Task SendCompletedMenu(long chatId, Session session, CancellationToken ct)
    {
        var labels = CompletedMenuLabels(session.Language ?? "en");
        var rows = new List<KeyboardButton[]>();
        var row = new List<string> { labels.changeAvec, labels.removeSignup };
        if (IsAdmin(session.UserId, session.Username, chatId, isPrivate: true))
        {
            row.Add(ExportLabel(session.Language ?? "en"));
            row.Add(BroadcastLabel(session.Language ?? "en"));
        }
        rows.Add(row.Select(l => new KeyboardButton(l)).ToArray());
        rows.Add(new[] { new KeyboardButton("/start") });
        var kb = new ReplyKeyboardMarkup(rows)
        { ResizeKeyboard = true, OneTimeKeyboard = false };

        await SendInLanguage(chatId, session.Language, (
            en: "You are signed up. Choose an option:",
            fi: "Olet ilmoittautunut. Valitse toiminto:"
        ), ct, kb);
    }

    internal static (string changeAvec, string removeSignup) CompletedMenuLabels(string lang)
    {
        return lang == "fi"
            ? ("Vaihda avecin nimi", "Peru ilmoittautuminen")
            : ("Change +1 name", "Remove signup");
    }

    internal static bool IsChangeAvecCommand(string lang, string input)
    {
        var v = input.Trim();
        if (lang == "fi") return string.Equals(v, "Vaihda avecin nimi", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "Vaihda avec", StringComparison.OrdinalIgnoreCase);
        return string.Equals(v, "Change +1 name", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "Change +1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "/avec", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsRemoveSignupCommand(string lang, string input)
    {
        var v = input.Trim();
        if (lang == "fi") return string.Equals(v, "Peru ilmoittautuminen", StringComparison.OrdinalIgnoreCase);
        return string.Equals(v, "Remove signup", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "/removeme", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "/signout", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ExportLabel(string lang) => lang == "fi" ? "Vie CSV" : "Export CSV";

    internal static bool IsExportCommand(string lang, string input)
    {
        var v = input.Trim();
        if (string.Equals(v, "/export", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(v, ExportLabel(lang), StringComparison.OrdinalIgnoreCase);
    }

    internal static string BroadcastLabel(string lang) => lang == "fi" ? "Lähetä kaikille" : "Broadcast";

    internal static bool IsBroadcastCommand(string lang, string input)
    {
        var v = input.Trim();
        if (string.Equals(v, "/broadcast", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(v, BroadcastLabel(lang), StringComparison.OrdinalIgnoreCase);
    }
}
