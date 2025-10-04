using System.Collections.Concurrent;
using Bot.Models;
using Bot.Services;
using Bot.Util;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bot;

public sealed partial class BotHandlers
{
    private readonly ITelegramBotClient _bot;
    private readonly IGuestRepository _repo;
    private readonly long? _adminUserId;
    private readonly string? _adminUsername;
    private readonly ConcurrentDictionary<long, Session> _sessions = new();
    private readonly string? _partyTextPath;
    private readonly string? _partyImagePath;
    private readonly IGroupAdminStore _groupAdmins;
    private readonly long _allowedGroupId;

    public BotHandlers(ITelegramBotClient bot, IGuestRepository repo, long? adminUserId = null, string? adminUsername = null, string? partyTextPath = null, string? partyImagePath = null, IGroupAdminStore? groupAdmins = null, long? allowedGroupId = null)
    {
        _bot = bot;
        _repo = repo;
        _adminUserId = adminUserId;
        _adminUsername = NormalizeHandle(adminUsername);
        _partyTextPath = partyTextPath;
        _partyImagePath = partyImagePath;
        _groupAdmins = groupAdmins ?? new GroupAdminStore();
        if (!allowedGroupId.HasValue) throw new ArgumentException("Single-group mode requires an allowedGroupId.");
        _allowedGroupId = allowedGroupId.Value;
    }

    public ReceiverOptions ReceiverOptions { get; } = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message || update.Message is null)
            return;

        var message = update.Message;
        var chatId = message.Chat.Id;
        var isPrivate = message.Chat.Type == ChatType.Private;
        // If single-group mode is configured, ignore non-private messages from other groups
        if (!isPrivate && chatId != _allowedGroupId)
            return;
        var text = message.Text?.Trim() ?? string.Empty;
        var session = _sessions.GetOrAdd(chatId, id => new Session { ChatId = id, Step = Step.AskLanguage });

        // Capture user identity for storage
        if (message.From is not null)
        {
            session.UserId = message.From.Id;
            session.Username = message.From.Username;
        }

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            // If already signed up (in session or storage), show menu instead of starting new
            if (session.Step == Step.Completed)
            {
                await SendCompletedMenu(chatId, session, ct);
                return;
            }

            // Check persisted state to prevent multiple signups across restarts
            var latest = await _repo.GetLatestForAsync(session.UserId ?? 0, chatId, ct);
            if (latest is not null && !string.Equals(latest.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            {
                session.Step = Step.Completed;
                session.Language = latest.Language;
                session.FullName = latest.FullName;
                session.WantsPlusOne = !string.IsNullOrWhiteSpace(latest.AvecFullName);
                session.AvecFullName = latest.AvecFullName;
                await SendCompletedMenu(chatId, session, ct);
                return;
            }

            session.Step = Step.AskLanguage;
            session.Language = null;
            session.FullName = null;
            session.WantsPlusOne = false;
            session.AvecFullName = null;
            await AskLanguage(chatId, ct);
            return;
        }

        // If user shares a contact while setting/changing +1, use it to fill the +1 name.
        if (message.Contact is not null)
        {
            var c = message.Contact;
            var full = (c.FirstName + " " + (c.LastName ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(full)) full = c.FirstName ?? string.Empty;

            if (session.Step == Step.AskAvecName)
            {
                session.WantsPlusOne = true;
                session.AvecFullName = NormalizeName(full);
                session.Step = Step.AskAvecHandle;
                await SendInLanguage(chatId, session.Language, (
                    en: "Optional: enter your +1's @handle (or type 'skip').",
                    fi: "Valinnainen: anna avicin @tunnus (tai kirjoita 'ohita')."
                ), ct);
                return;
            }
            if (session.Step == Step.ChangeAvecName)
            {
                session.WantsPlusOne = true;
                session.AvecFullName = NormalizeName(full);
                session.Step = Step.ChangeAvecHandle;
                await SendInLanguage(chatId, session.Language, (
                    en: "Optional: enter your +1's @handle (or type 'skip').",
                    fi: "Valinnainen: anna avicin @tunnus (tai kirjoita 'ohita')."
                ), ct);
                return;
            }
        }

        if (text.Equals("/export", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
            {
                await _bot.SendTextMessageAsync(chatId,
                    session.Language == "fi" ? "Ei oikeuksia tähän komentoon." : "You are not authorized to use this command.",
                    cancellationToken: ct);
                return;
            }
            await ExportAndSend(chatId, session, ct);
            return;
        }

        if (text.Equals("/broadcast", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
            {
                await _bot.SendTextMessageAsync(chatId,
                    session.Language == "fi" ? "Ei oikeuksia tähän komentoon." : "You are not authorized to use this command.",
                    cancellationToken: ct);
                return;
            }
            if (message.ReplyToMessage is null)
            {
                await SendInLanguage(chatId, session.Language, (
                    en: "Reply to the message you want to send, then type /broadcast.",
                    fi: "Vastaa viestiin jonka haluat lähettää ja kirjoita /broadcast."
                ), ct);
                return;
            }
            await BroadcastAsync(chatId, message.ReplyToMessage, session, ct);
            return;
        }

        // Allow admin export/broadcast via button labels at any step
        if (IsExportCommand(session.Language ?? "en", text))
        {
            if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
            {
                await _bot.SendTextMessageAsync(chatId,
                    session.Language == "fi" ? "Ei oikeuksia tähän toimintoon." : "You are not authorized to use this option.",
                    cancellationToken: ct);
                return;
            }
            await ExportAndSend(chatId, session, ct);
            return;
        }

        if (IsBroadcastCommand(session.Language ?? "en", text))
        {
            if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
            {
                await _bot.SendTextMessageAsync(chatId,
                    session.Language == "fi" ? "Ei oikeuksia tähän toimintoon." : "You are not authorized to use this option.",
                    cancellationToken: ct);
                return;
            }
            if (message.ReplyToMessage is null)
            {
                await SendInLanguage(chatId, session.Language, (
                    en: "Reply to the message you want to send, then tap Broadcast.",
                    fi: "Vastaa viestiin jonka haluat lähettää ja paina Lähetä kaikille."
                ), ct);
                return;
            }
            await BroadcastAsync(chatId, message.ReplyToMessage, session, ct);
            return;
        }

        if (text.Equals("/whoami", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
            {
                await _bot.SendTextMessageAsync(chatId,
                    session.Language == "fi" ? "Ei oikeuksia tähän komentoon." : "You are not authorized to use this command.",
                    cancellationToken: ct);
                return;
            }
            var uname = session.Username is null ? "-" : "@" + session.Username;
            await _bot.SendTextMessageAsync(chatId,
                $"UserId: {session.UserId}\nChatId: {chatId}\nUsername: {uname}",
                cancellationToken: ct);
            return;
        }

        // Superadmin-only (in private chat): manage group admins
        if (isPrivate && text.StartsWith("/groupadmins", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsSuperAdmin(session.UserId, session.Username))
            {
                await _bot.SendTextMessageAsync(chatId, "Not authorized.", cancellationToken: ct);
                return;
            }
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await _bot.SendTextMessageAsync(chatId,
                    "Usage:\n/groupadmins list\n/groupadmins add <userId>\n/groupadmins remove <userId>",
                    cancellationToken: ct);
                return;
            }
            var cmd = parts[1].ToLowerInvariant();
            if (cmd == "list")
            {
                var set = await _groupAdmins.GetAdminsAsync(_allowedGroupId, ct);
                await _bot.SendTextMessageAsync(chatId, set.Count == 0 ? "(empty)" : string.Join("\n", set), cancellationToken: ct);
                return;
            }
            if (cmd == "add" || cmd == "remove")
            {
                if (parts.Length == 3 && long.TryParse(parts[2], out var u))
                {
                    if (cmd == "add")
                    {
                        var ok = await _groupAdmins.AddAdminAsync(_allowedGroupId, u, ct);
                        await _bot.SendTextMessageAsync(chatId, ok ? "Added" : "Already present", cancellationToken: ct);
                    }
                    else
                    {
                        var ok = await _groupAdmins.RemoveAdminAsync(_allowedGroupId, u, ct);
                        await _bot.SendTextMessageAsync(chatId, ok ? "Removed" : "Not found", cancellationToken: ct);
                    }
                    return;
                }
                await _bot.SendTextMessageAsync(chatId, "Usage: /groupadmins add <userId> | /groupadmins remove <userId>", cancellationToken: ct);
                return;
            }
            await _bot.SendTextMessageAsync(chatId, "Invalid syntax. See /groupadmins list|add|remove", cancellationToken: ct);
            return;
        }

        // Completed menu button handling (localized labels)
        if (session.Step == Step.Completed)
        {
            if (IsChangeAvecCommand(session.Language ?? "en", text))
            {
                session.Step = Step.ChangeAvecName;
                await SendInLanguage(chatId, session.Language, (
                    en: "Send your +1's full name, or type 'none' to remove.",
                    fi: "Lähetä avecin koko nimi tai kirjoita 'ei' poistaaksesi."
                ), ct);
                return;
            }
            if (IsRemoveSignupCommand(session.Language ?? "en", text))
            {
                await SignOut(chatId, session, ct);
                return;
            }
            if (IsExportCommand(session.Language ?? "en", text))
            {
                if (!IsAdmin(session.UserId, session.Username, chatId, isPrivate))
                {
                    await _bot.SendTextMessageAsync(chatId,
                        session.Language == "fi" ? "Ei oikeuksia tähän toimintoon." : "You are not authorized to use this option.",
                        cancellationToken: ct);
                    return;
                }
                await ExportAndSend(chatId, session, ct);
                return;
            }
        }

        if (text.Equals("/info", StringComparison.OrdinalIgnoreCase))
        {
            await ShowPartyInfo(chatId, session, ct);
            return;
        }

        // In non-private chats, ignore interactive flows
        if (!isPrivate)
            return;

        if (text.Equals("/removeme", StringComparison.OrdinalIgnoreCase) || text.Equals("/signout", StringComparison.OrdinalIgnoreCase))
        {
            if (session.Step != Step.Completed)
            {
                await SendInLanguage(chatId, session.Language, (
                    en: "Complete your signup first. Send /start.",
                    fi: "Viimeistele ilmoittautuminen ensin. Lähetä /start."
                ), ct);
                return;
            }

            await SignOut(chatId, session, ct);
            return;
        }

        if (text.Equals("/avec", StringComparison.OrdinalIgnoreCase))
        {
            if (session.Step != Step.Completed)
            {
                await SendInLanguage(chatId, session.Language, (
                    en: "Finish signup first. Send /start.",
                    fi: "Viimeistele ilmoittautuminen ensin. Lähetä /start."
                ), ct);
                return;
            }

            session.Step = Step.ChangeAvecName;
            await SendInLanguage(chatId, session.Language, (
                en: "Send your +1's full name, or type 'none' to remove.",
                fi: "Lähetä avecin koko nimi tai kirjoita 'ei' poistaaksesi."
            ), ct);
            return;
        }

        switch (session.Step)
        {
            case Step.AskLanguage:
                await OnLanguage(chatId, session, text, ct);
                break;
            case Step.AskAction:
                await OnAction(chatId, session, text, ct);
                break;
            case Step.AskFullName:
                await OnFullName(chatId, session, text, ct);
                break;
            case Step.AskPlusOne:
                await OnPlusOne(chatId, session, text, ct);
                break;
            case Step.AskAvecName:
                await OnAvecName(chatId, session, text, ct);
                break;
            case Step.AskAvecHandle:
                await OnAvecHandle(chatId, session, text, ct);
                break;
            case Step.ChangeAvecName:
                await OnChangeAvecName(chatId, session, text, ct);
                break;
            case Step.ChangeAvecHandle:
                await OnChangeAvecHandle(chatId, session, text, ct);
                break;
            case Step.Completed:
                await SendCompletedMenu(chatId, session, ct);
                break;
        }
    }

    private async Task ExportAndSend(long chatId, Session session, CancellationToken ct)
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var outPath = Path.Combine(dataDir, $"rsvps-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        await CsvExporter.ExportAsync(outPath, null, ct);
        await using var fs = System.IO.File.OpenRead(outPath);
        var caption = session.Language == "fi" ? "RSVP-vienti valmis" : "RSVP export ready";
        await _bot.SendDocumentAsync(chatId, Telegram.Bot.Types.InputFile.FromStream(fs, Path.GetFileName(outPath)), caption: caption, cancellationToken: ct);
    }

    private async Task BroadcastAsync(long adminChatId, Telegram.Bot.Types.Message source, Session session, CancellationToken ct)
    {
        // Build latest active entries and username->chat map
        var inputPath = Path.Combine(AppContext.BaseDirectory, "data", "rsvps.jsonl");
        var latest = new Dictionary<long, GuestRecord>();
        var usernameToChat = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (System.IO.File.Exists(inputPath))
        {
            using var reader = new StreamReader(System.IO.File.OpenRead(inputPath));
            string? line;
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = System.Text.Json.JsonSerializer.Deserialize<GuestRecord>(line, options);
                    if (rec is null) continue;
                    var key = rec.UserId != 0 ? rec.UserId : rec.ChatId;
                    latest[key] = rec;
                    if (!string.IsNullOrWhiteSpace(rec.TelegramUsername))
                        usernameToChat[rec.TelegramUsername!] = rec.ChatId;
                }
                catch { }
            }
        }

        var targets = new HashSet<long>();
        foreach (var rec in latest.Values)
        {
            if (string.Equals(rec.Status, "Deleted", StringComparison.OrdinalIgnoreCase)) continue;
            targets.Add(rec.ChatId);
        }
        // Include avec usernames if we can resolve to a chatId
        foreach (var rec in latest.Values)
        {
            if (string.Equals(rec.Status, "Deleted", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(rec.AvecUsername) && usernameToChat.TryGetValue(rec.AvecUsername!, out var chat))
                targets.Add(chat);
        }

        int ok = 0, fail = 0;
        foreach (var target in targets)
        {
            try
            {
                await _bot.CopyMessageAsync(target, adminChatId, source.MessageId, cancellationToken: ct);
                ok++;
                await Task.Delay(40, ct);
            }
            catch
            {
                fail++;
            }
        }
        await SendInLanguage(adminChatId, session.Language, (
            en: $"Broadcast done. Sent: {ok}, failed: {fail}",
            fi: $"Lähetys valmis. Onnistui: {ok}, epäonnistui: {fail}"
        ), ct);
    }

    private bool IsAdmin(long? userId, string? username, long chatId, bool isPrivate)
    {
        // Super admin numeric ID
        if (_adminUserId.HasValue && userId.HasValue && userId.Value == _adminUserId.Value)
            return true;

        // Super admin username (when available)
        if (!string.IsNullOrWhiteSpace(_adminUsername))
        {
            var userHandle = NormalizeHandle(username);
            if (!string.IsNullOrEmpty(userHandle) && string.Equals(userHandle, _adminUsername, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Group-specific admins
        if (!isPrivate && userId.HasValue)
        {
            var set = _groupAdmins.GetAdminsAsync(_allowedGroupId).GetAwaiter().GetResult();
            if (set.Contains(userId.Value)) return true;
        }

        return false;
    }

    private bool IsSuperAdmin(long? userId, string? username)
    {
        if (_adminUserId.HasValue && userId.HasValue && userId.Value == _adminUserId.Value)
            return true;
        if (!string.IsNullOrWhiteSpace(_adminUsername))
        {
            var userHandle = NormalizeHandle(username);
            if (!string.IsNullOrEmpty(userHandle) && string.Equals(userHandle, _adminUsername, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? NormalizeHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return null;
        var h = handle.Trim();
        if (h.StartsWith("@")) h = h[1..];
        return h.ToLowerInvariant();
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var errMsg = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: [{apiEx.ErrorCode}] {apiEx.Message}",
            _ => exception.ToString()
        };
        Console.Error.WriteLine(errMsg);
        return Task.CompletedTask;
    }

    public async Task AskLanguage(long chatId, CancellationToken ct)
    {
        _sessions.TryGetValue(chatId, out var session);
        var rows = new List<KeyboardButton[]> { new [] { new KeyboardButton("fi"), new KeyboardButton("en") } };
        if (session is not null && IsAdmin(session.UserId, session.Username, chatId, isPrivate: true))
        {
            // Only show Broadcast here (not Export)
            rows.Add(new[] { new KeyboardButton(BroadcastLabel(session.Language ?? "en")) });
        }
        var kb = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };

        await _bot.SendTextMessageAsync(chatId,
            "Choose language / Valitse kieli: fi or en",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    private async Task SendInLanguage(long chatId, string? lang, (string en, string fi) msg, CancellationToken ct, IReplyMarkup? replyMarkup = null)
    {
        var text = (lang == "fi") ? msg.fi : msg.en;
        await _bot.SendTextMessageAsync(chatId, text, replyMarkup: replyMarkup, cancellationToken: ct);
    }

    internal async Task ShowPartyInfo(long chatId, Session session, CancellationToken ct)
    {
        // Resolve relative to the executable's directory (AppContext.BaseDirectory)
        var baseDir = AppContext.BaseDirectory;
        string ResolveAtBase(string path) => Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);

        static SixLabors.ImageSharp.Formats.IImageEncoder? EncoderForExtension(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder(),
                ".png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
                ".gif" => new SixLabors.ImageSharp.Formats.Gif.GifEncoder(),
                ".bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
                _ => null
            };
        }

        async Task SendTextAsync()
        {
            var path = _partyTextPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var abs = ResolveAtBase(path);
                if (System.IO.File.Exists(abs))
                {
                    try
                    {
                        var text = await System.IO.File.ReadAllTextAsync(abs, ct);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await _bot.SendTextMessageAsync(chatId, text, cancellationToken: ct);
                            return;
                        }
                    }
                    catch { /* ignore and fall back */ }
                }
            }
            await SendInLanguage(chatId, session.Language, (
                en: "Party info is not available.",
                fi: "Juhlatietoja ei ole saatavilla."
            ), ct);
        }

        var imgPath = _partyImagePath;
        if (!string.IsNullOrWhiteSpace(imgPath))
        {
            var absImg = ResolveAtBase(imgPath);

            if (!System.IO.File.Exists(absImg))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absImg)!);
                    using var img = new Image<Rgba32>(400, 200, new Rgba32(255, 200, 50));
                    // add some noisy pixels
                    for (int y = 20; y < 180; y += 10)
                    for (int x = 20; x < 380; x += 10)
                        img[x, y] = new Rgba32((byte)(x % 255), (byte)(y % 255), (byte)((x+y) % 255));
                    var enc = EncoderForExtension(absImg);
                    if (enc is null)
                    {
                        // Fallback to PNG if extension unrecognized
                        var pngPath = Path.ChangeExtension(absImg, ".png");
                        await img.SaveAsync(pngPath, new SixLabors.ImageSharp.Formats.Png.PngEncoder(), ct);
                        absImg = pngPath;
                    }
                    else
                    {
                        await img.SaveAsync(absImg, enc, ct);
                    }
                }
                catch { /* ignore generation errors */ }
            }

            if (System.IO.File.Exists(absImg))
            {
                await using var fs = System.IO.File.OpenRead(absImg);
                await _bot.SendPhotoAsync(chatId, Telegram.Bot.Types.InputFile.FromStream(fs, Path.GetFileName(absImg)), cancellationToken: ct);
                await SendTextAsync();
                return;
            }
        }

        await SendTextAsync();
    }

    // Test utility to seed a session
    internal void SetSession(long chatId, Session session) => _sessions[chatId] = session;
}
