using Bot.Models;
using Bot.Services;

using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot;

public sealed partial class BotHandlers
{
    internal async Task OnLanguage(long chatId, Session session, string text, CancellationToken ct)
    {
        if (!TryNormalizeLanguage(text, out var lang))
        {
            var rows = new List<KeyboardButton[]> { new[] { new KeyboardButton("fi"), new KeyboardButton("en") } };
            if (IsAdmin(session.UserId, session.Username, chatId, isPrivate: true))
            {
                // Only show Broadcast here (not Export)
                rows.Add([new KeyboardButton(BroadcastLabel(session.Language ?? "en"))]);
            }
            var kb = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };

            await _bot.SendTextMessageAsync(chatId,
                "Choose your language / Valitse kieli",
                replyMarkup: kb,
                cancellationToken: ct);
            return;
        }

        session.Language = lang;
        session.Step = Step.AskAction;
        await SendActionMenu(chatId, session, ct);
    }

    internal async Task OnAction(long chatId, Session session, string text, CancellationToken ct)
    {
        if (IsExportCommand(session.Language ?? "en", text))
        {
            if (IsAdmin(session.UserId, session.Username, chatId, isPrivate: true))
            {
                await ExportAndSend(chatId, session, ct);
            }
            else
            {
                await SendInLanguage(chatId, session.Language, ("You are not authorized.", "Ei oikeuksia."), ct);
            }

            return;
        }

        var lang = session.Language ?? "en";
        if (IsActionSignUp(lang, text))
        {
            session.Step = Step.AskFullName;
            await SendInLanguage(chatId, lang, (
                en: "Please enter your full name (first and last):",
                fi: "Kirjoita koko nimesi (etu- ja sukunimi):"
            ), ct);
            return;
        }
        if (IsActionPartyInfo(lang, text) || text.Equals("/info", StringComparison.OrdinalIgnoreCase))
        {
            if (HasPartyInfo())
            {
                await ShowPartyInfo(chatId, session, ct);
            }
            // Re-show menu regardless; if no info, the button isn't shown
            await SendActionMenu(chatId, session, ct);
            return;
        }

        // Re-show if unknown
        await SendActionMenu(chatId, session, ct);
    }

    internal async Task OnFullName(long chatId, Session session, string text, CancellationToken ct)
    {
        if (!LooksLikeRealName(text))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Enter a real first and last name (letters only).",
                fi: "Anna oikea etu- ja sukunimi (vain kirjaimet)."
            ), ct);
            return;
        }

        session.FullName = NormalizeName(text);
        session.Step = Step.AskPlusOne;

        var kb = new ReplyKeyboardMarkup(
        [
            [session.Language == "fi" ? "kyllä" : "yes", session.Language == "fi" ? "ei" : "no"]
        ])
        { ResizeKeyboard = true, OneTimeKeyboard = true };

        await SendInLanguage(chatId, session.Language, (
            en: "Do you want a +1 (avec)? yes/no",
            fi: "Haluatko avecin? kyllä/ei"
        ), ct, kb);
    }

    internal async Task OnPlusOne(long chatId, Session session, string text, CancellationToken ct)
    {
        if (!ParseYesNo(session.Language ?? "en", text, out var yes))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Please answer yes or no.",
                fi: "Vastaa kyllä tai ei."
            ), ct);
            return;
        }

        session.WantsPlusOne = yes;
        if (yes)
        {
            session.Step = Step.AskAvecName;
            await SendInLanguage(chatId, session.Language, (
                en: "Enter your +1's full name (first and last), or share their contact card:",
                fi: "Anna avecin koko nimi (etu- ja sukunimi), tai jaa hänen yhteystietonsa:"
            ), ct);
        }
        else
        {
            await SaveAndConfirm(chatId, session, ct);
        }
    }

    internal async Task OnAvecName(long chatId, Session session, string text, CancellationToken ct)
    {
        // Allow canceling +1 during the flow
        if (IsCancelAvec(session.Language ?? "en", text))
        {
            session.WantsPlusOne = false;
            session.AvecFullName = null;
            session.AvecUsername = null;
            await SaveAndConfirm(chatId, session, ct);
            return;
        }

        if (!LooksLikeRealName(text))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Enter a real first and last name for your +1.",
                fi: "Anna avecille oikea etu- ja sukunimi."
            ), ct);
            return;
        }

        session.AvecFullName = NormalizeName(text);
        session.Step = Step.AskAvecHandle;
        await SendInLanguage(chatId, session.Language, (
            en: "Optional: enter your +1's @handle (or type 'skip').",
            fi: "Valinnainen: anna avicin @tunnus (tai kirjoita 'ohita')."
        ), ct);
    }

    internal async Task OnAvecHandle(long chatId, Session session, string text, CancellationToken ct)
    {
        var v = text.Trim();
        if (string.Equals(v, "skip", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "ohita", StringComparison.OrdinalIgnoreCase))
        {
            session.AvecUsername = null;
            await SaveAndConfirm(chatId, session, ct);
            return;
        }
        if (!TryNormalizeHandle(v, out var handle))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Invalid handle. Use @name (5-32 letters/digits/_), or type 'skip'.",
                fi: "Virheellinen tunnus. Käytä @nimi (5-32 merkkiä), tai kirjoita 'ohita'."
            ), ct);
            return;
        }
        session.AvecUsername = handle;
        await SaveAndConfirm(chatId, session, ct);
    }

    internal async Task OnChangeAvecName(long chatId, Session session, string text, CancellationToken ct)
    {
        if (IsCancelAvec(session.Language ?? "en", text))
        {
            session.WantsPlusOne = false;
            session.AvecFullName = null;
            await SaveAndConfirm(chatId, session, ct);
            return;
        }

        if (!LooksLikeRealName(text))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Enter a real first and last name, or 'none' to remove.",
                fi: "Anna oikea etu- ja sukunimi tai 'ei' poistaaksesi."
            ), ct);
            return;
        }

        session.WantsPlusOne = true;
        session.AvecFullName = NormalizeName(text);
        session.Step = Step.ChangeAvecHandle;
        await SendInLanguage(chatId, session.Language, (
            en: "Optional: enter your +1's @handle (or type 'skip').",
            fi: "Valinnainen: anna avicin @tunnus (tai kirjoita 'ohita')."
        ), ct);
    }

    internal async Task OnChangeAvecHandle(long chatId, Session session, string text, CancellationToken ct)
    {
        var v = text.Trim();
        if (string.Equals(v, "skip", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "ohita", StringComparison.OrdinalIgnoreCase))
        {
            await SaveAndConfirm(chatId, session, ct);
            await SendCompletedMenu(chatId, session, ct);
            return;
        }
        if (!TryNormalizeHandle(v, out var handle))
        {
            await SendInLanguage(chatId, session.Language, (
                en: "Invalid handle. Use @name (5-32 letters/digits/_), or type 'skip'.",
                fi: "Virheellinen tunnus. Käytä @nimi (5-32 merkkiä), tai kirjoita 'ohita'."
            ), ct);
            return;
        }
        session.AvecUsername = handle;
        await SaveAndConfirm(chatId, session, ct);
        await SendCompletedMenu(chatId, session, ct);
    }

    internal async Task SaveAndConfirm(long chatId, Session session, CancellationToken ct)
    {
        session.Step = Step.Completed;

        var record = new GuestRecord(
            ChatId: chatId,
            UserId: session.UserId ?? 0,
            Timestamp: DateTimeOffset.UtcNow,
            Language: session.Language ?? "en",
            FullName: session.FullName ?? string.Empty,
            AvecFullName: session.WantsPlusOne ? session.AvecFullName : null,
            TelegramUsername: session.Username,
            AvecUsername: session.WantsPlusOne ? session.AvecUsername : null,
            Status: "Active"
        );

        await _repo.AppendAsync(record, ct);

        // If this user was previously listed as someone else's +1 by @handle, clear those links
        if (!string.IsNullOrWhiteSpace(session.Username))
        {
            var h = session.Username!;
            if (h.StartsWith("@"))
            {
                h = h[1..];
            }

            h = h.ToLowerInvariant();
            await _repo.RemoveAvecByHandleAsync(h, ct);
        }

        var summaryEn = $"Thanks! Saved.\nName: {record.FullName}\nAvec: {(record.AvecFullName ?? "—")}\nLanguage: {record.Language}.";
        var summaryFi = $"Kiitos! Tallennettu.\nNimi: {record.FullName}\nAvec: {(record.AvecFullName ?? "—")}\nKieli: {record.Language}.";

        await SendInLanguage(chatId, session.Language, (summaryEn, summaryFi), ct);
        await SendCompletedMenu(chatId, session, ct);
    }

    internal async Task SignOut(long chatId, Session session, CancellationToken ct)
    {
        var record = new GuestRecord(
            ChatId: chatId,
            UserId: session.UserId ?? 0,
            Timestamp: DateTimeOffset.UtcNow,
            Language: session.Language ?? "en",
            FullName: session.FullName ?? string.Empty,
            AvecFullName: null,
            TelegramUsername: session.Username,
            AvecUsername: null,
            Status: "Deleted"
        );

        await _repo.AppendAsync(record, ct);

        await SendInLanguage(chatId, session.Language, (
            en: "Your signup has been removed. Send /start to sign up again.",
            fi: "Ilmoittautuminen poistettu. Lähetä /start ilmoittautuaksesi uudelleen."
        ), ct);

        session.Step = Step.Completed;
        session.WantsPlusOne = false; session.AvecFullName = null;

        // Offer a Start button to re-register
        var kb = new ReplyKeyboardMarkup([["/start"]])
        { ResizeKeyboard = true, OneTimeKeyboard = false };
        await _bot.SendTextMessageAsync(chatId,
            session.Language == "fi" ? "Voit aloittaa alusta painamalla /start." : "You can start over by pressing /start.",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    internal async Task SendActionMenu(long chatId, Session session, CancellationToken ct)
    {
        var lang = session.Language ?? "en";
        (var signUp, var info) = ActionLabels(lang);
        var rows = new List<Telegram.Bot.Types.ReplyMarkups.KeyboardButton[]>();
        var firstRow = new List<Telegram.Bot.Types.ReplyMarkups.KeyboardButton>
        {
            new(signUp)
        };
        if (HasPartyInfo())
        {
            firstRow.Add(new Telegram.Bot.Types.ReplyMarkups.KeyboardButton(info));
        }
        rows.Add([.. firstRow]);
        if (IsAdmin(session.UserId, session.Username, chatId, isPrivate: true))
        {
            rows.Add([new Telegram.Bot.Types.ReplyMarkups.KeyboardButton(BotHandlers.ExportLabel(lang)), new Telegram.Bot.Types.ReplyMarkups.KeyboardButton(BotHandlers.BroadcastLabel(lang))]);
        }
        var kb = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = false };
        await SendInLanguage(chatId, session.Language, (
            en: "What would you like to do?",
            fi: "Mitä haluaisit tehdä?"
        ), ct, kb);
    }

    internal static (string signUp, string info) ActionLabels(string lang)
        => lang == "fi" ? ("Ilmoittaudu", "Tapahtuman tiedot") : ("Sign up", "Party info");

    internal static bool IsActionSignUp(string lang, string input)
    {
        var v = input.Trim();
        if (lang == "fi")
        {
            return string.Equals(v, "Ilmoittaudu", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(v, "Sign up", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsActionPartyInfo(string lang, string input)
    {
        var v = input.Trim();
        if (lang == "fi")
        {
            return string.Equals(v, "Tapahtuman tiedot", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(v, "Party info", StringComparison.OrdinalIgnoreCase);
    }
}
