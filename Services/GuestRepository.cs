using System.Text;
using System.Text.Json;
using Bot.Models;

namespace Bot.Services;

public interface IGuestRepository
{
    Task AppendAsync(GuestRecord record, CancellationToken ct = default);
    Task<GuestRecord?> GetLatestForAsync(long userId, long chatId, CancellationToken ct = default);
    Task<int> RemoveAvecByHandleAsync(string normalizedHandle, CancellationToken ct = default);
}

public sealed class GuestRepository : IGuestRepository
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string _path;

    public GuestRepository(string? path = null)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(baseDir);
        _path = path ?? Path.Combine(baseDir, "rsvps.jsonl");
    }

    public async Task AppendAsync(GuestRecord record, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(record);
        await Gate.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_path, json + Environment.NewLine, Encoding.UTF8, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<GuestRecord?> GetLatestForAsync(long userId, long chatId, CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return null;
        await using var inStream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(inStream, Encoding.UTF8);
        string? line;
        GuestRecord? last = null;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var rec = JsonSerializer.Deserialize<GuestRecord>(line, options);
                if (rec is null) continue;
                if (userId != 0)
                {
                    if (rec.UserId == userId) last = rec;
                }
                else
                {
                    if (rec.ChatId == chatId) last = rec;
                }
            }
            catch { /* skip malformed */ }
        }
        return last;
    }

    public async Task<int> RemoveAvecByHandleAsync(string normalizedHandle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedHandle)) return 0;
        if (!File.Exists(_path)) return 0;

        // Build latest by key
        var latest = new Dictionary<long, GuestRecord>();
        await using (var inStream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(inStream, Encoding.UTF8))
        {
            string? line;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = JsonSerializer.Deserialize<GuestRecord>(line, options);
                    if (rec is null) continue;
                    var key = rec.UserId != 0 ? rec.UserId : rec.ChatId;
                    latest[key] = rec;
                }
                catch { }
            }
        }

        var affected = latest.Values
            .Where(r => !string.Equals(r.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            .Where(r => !string.IsNullOrWhiteSpace(r.AvecUsername) && string.Equals(r.AvecUsername, normalizedHandle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (affected.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        await Gate.WaitAsync(ct);
        try
        {
            foreach (var owner in affected)
            {
                var updated = new GuestRecord(
                    ChatId: owner.ChatId,
                    UserId: owner.UserId,
                    Timestamp: now,
                    Language: owner.Language,
                    FullName: owner.FullName,
                    AvecFullName: null,
                    TelegramUsername: owner.TelegramUsername,
                    AvecUsername: null,
                    Status: "Active"
                );
                var json = JsonSerializer.Serialize(updated);
                await File.AppendAllTextAsync(_path, json + Environment.NewLine, Encoding.UTF8, ct);
            }
        }
        finally
        {
            Gate.Release();
        }

        return affected.Count;
    }
}
