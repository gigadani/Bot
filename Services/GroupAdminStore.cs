using System.Text.Json;

namespace Bot.Services;

public interface IGroupAdminStore
{
    Task<HashSet<long>> GetAdminsAsync(long groupChatId, CancellationToken ct = default);
    Task<bool> AddAdminAsync(long groupChatId, long userId, CancellationToken ct = default);
    Task<bool> RemoveAdminAsync(long groupChatId, long userId, CancellationToken ct = default);
    Task<Dictionary<long, HashSet<long>>> ListAsync(CancellationToken ct = default);
}

public sealed class GroupAdminStore : IGroupAdminStore
{
    private readonly string _path;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public GroupAdminStore(string? path = null)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(baseDir);
        _path = path ?? Path.Combine(baseDir, "group_admins.json");
    }

    public async Task<HashSet<long>> GetAdminsAsync(long groupChatId, CancellationToken ct = default)
    {
        var map = await LoadAsync(ct);
        return map.TryGetValue(groupChatId, out var set) ? new HashSet<long>(set) : new HashSet<long>();
    }

    public async Task<bool> AddAdminAsync(long groupChatId, long userId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var map = await LoadAsync(ct);
            if (!map.TryGetValue(groupChatId, out var set)) { set = new HashSet<long>(); map[groupChatId] = set; }
            var added = set.Add(userId);
            if (added) await SaveAsync(map, ct);
            return added;
        }
        finally { Gate.Release(); }
    }

    public async Task<bool> RemoveAdminAsync(long groupChatId, long userId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var map = await LoadAsync(ct);
            if (!map.TryGetValue(groupChatId, out var set)) return false;
            var removed = set.Remove(userId);
            if (removed)
            {
                if (set.Count == 0) map.Remove(groupChatId);
                await SaveAsync(map, ct);
            }
            return removed;
        }
        finally { Gate.Release(); }
    }

    public Task<Dictionary<long, HashSet<long>>> ListAsync(CancellationToken ct = default) => LoadAsync(ct);

    private async Task<Dictionary<long, HashSet<long>>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            var map = await JsonSerializer.DeserializeAsync<Dictionary<long, HashSet<long>>>(s, cancellationToken: ct);
            return map ?? new();
        }
        catch { return new(); }
    }

    private async Task SaveAsync(Dictionary<long, HashSet<long>> map, CancellationToken ct)
    {
        await using var s = File.Open(_path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(s, map, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}

