using Bot;
using Bot.Services;
using Bot.Util;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

// Build configuration: appsettings.json + user-secrets + env vars
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .Build();

// Command mode: `export [out.csv] [in.jsonl]`
if (args is { Length: > 0 } && string.Equals(args[0], "export", StringComparison.OrdinalIgnoreCase))
{
    var outPath = args.Length > 1 ? args[1] : null;
    var inPath = args.Length > 2 ? args[2] : null;
    return await CsvExporter.ExportAsync(outPath, inPath);
}

var token = configuration["Telegram:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Missing TELEGRAM_BOT_TOKEN environment variable.");
    Console.Error.WriteLine("Set Telegram:Token in appsettings.json or via user-secrets:");
    Console.Error.WriteLine("  dotnet user-secrets set \"Telegram:Token\" \"123:abc\"");
    Console.Error.WriteLine("Or run export mode: dotnet run -- export [out.csv] [in.jsonl]");
    return 1;
}

var botClient = new TelegramBotClient(token);
var me = await botClient.GetMeAsync();
Console.WriteLine($"Starting bot @{me.Username} (id {me.Id})...");

var adminSetting = configuration["Admin:UserId"];
long? adminUserId = null;
string? adminUsername = null;
if (!string.IsNullOrWhiteSpace(adminSetting))
{
    if (long.TryParse(adminSetting, out var parsedAdmin)) adminUserId = parsedAdmin;
    else adminUsername = adminSetting.Trim();
}

var repo = new GuestRepository();
var groupAdminStore = new GroupAdminStore();

// Single-group mode is mandatory
var groupIdSetting = configuration["Group:Id"];
if (string.IsNullOrWhiteSpace(groupIdSetting) || !long.TryParse(groupIdSetting, out var parsedGroupId))
{
    Console.Error.WriteLine("Missing or invalid Group:Id. Configure the numeric Telegram group ID in appsettings.json or environment.");
    Console.Error.WriteLine("Example:  dotnet user-secrets set \"Group:Id\" \"-1001234567890\" ");
    return 1;
}
long allowedGroupId = parsedGroupId;
var handlers = new BotHandlers(
    botClient,
    repo,
    adminUserId,
    adminUsername,
    configuration["Party:InfoTextPath"],
    configuration["Party:InfoImagePath"],
    groupAdminStore,
    allowedGroupId
);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

botClient.StartReceiving(
    updateHandler: handlers.HandleUpdateAsync,
    pollingErrorHandler: handlers.HandleErrorAsync,
    receiverOptions: handlers.ReceiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Bot is running. Press Ctrl+C to stop.");
await Task.Delay(-1, cts.Token).ContinueWith(_ => { });
return 0;
