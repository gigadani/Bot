using System.Text;
using System.Text.Json;
using Bot.Models;

namespace Bot.Util;

public static class CsvExporter
{
    public static async Task<int> ExportAsync(string? outputPath = null, string? inputPath = null, CancellationToken ct = default)
    {
        inputPath ??= Path.Combine(AppContext.BaseDirectory, "data", "rsvps.jsonl");
        outputPath ??= Path.Combine(AppContext.BaseDirectory, "data", "rsvps.csv");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (!File.Exists(inputPath))
        {
            await File.WriteAllTextAsync(outputPath, "ChatId,UserId,Username,Language,FullName,AvecFullName,AvecUsername,Timestamp\n", Encoding.UTF8, ct);
            Console.WriteLine($"No input found. Created empty CSV at {outputPath}.");
            return 0;
        }

        await using var inStream = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(inStream, Encoding.UTF8);

        // Collapse to latest record per user/chat
        var latest = new Dictionary<long, GuestRecord>();
        string? line;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            GuestRecord? rec = null;
            try
            {
                rec = JsonSerializer.Deserialize<GuestRecord>(line, options);
            }
            catch { }
            if (rec is null) continue;

            var key = rec.UserId != 0 ? rec.UserId : rec.ChatId;
            latest[key] = rec;
        }

        await using var outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(outStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteLineAsync("ChatId,UserId,Username,Language,FullName,AvecFullName,AvecUsername,Timestamp");

        foreach (var rec in latest.Values.Where(r => !string.Equals(r.Status, "Deleted", StringComparison.OrdinalIgnoreCase)))
        {
            var row = string.Join(',', new[]
            {
                Csv(rec.ChatId.ToString()),
                Csv(rec.UserId.ToString()),
                Csv(rec.TelegramUsername ?? string.Empty),
                Csv(rec.Language),
                Csv(rec.FullName),
                Csv(rec.AvecFullName ?? string.Empty),
                Csv(rec.AvecUsername ?? string.Empty),
                Csv(rec.Timestamp.ToString("O"))
            });
            await writer.WriteLineAsync(row);
        }

        await writer.FlushAsync();
        Console.WriteLine($"Exported CSV to {outputPath}.");
        return 0;
    }

    private static string Csv(string input)
    {
        var needsQuote = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
        if (!needsQuote) return input;
        var esc = input.Replace("\"", "\"\"");
        return $"\"{esc}\"";
    }
}
