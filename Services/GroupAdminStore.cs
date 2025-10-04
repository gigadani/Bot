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
        Dictionary<long, HashSet<long>> map = await LoadAsync(ct);
        return map.TryGetValue(groupChatId, out HashSet<long>? set) ? new HashSet<long>(set) : [];
    }

    public async Task<bool> AddAdminAsync(long groupChatId, long userId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            Dictionary<long, HashSet<long>> map = await LoadAsync(ct);
            if (!map.TryGetValue(groupChatId, out HashSet<long>? set)) { set = []; map[groupChatId] = set; }
            var added = set.Add(userId);
            if (added)
            {
                await SaveAsync(map, ct);
            }

            return added;
        }
        finally { Gate.Release(); }
    }

    public async Task<bool> RemoveAdminAsync(long groupChatId, long userId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            Dictionary<long, HashSet<long>> map = await LoadAsync(ct);
            if (!map.TryGetValue(groupChatId, out HashSet<long>? set))
            {
                return false;
            }

            var removed = set.Remove(userId);
            if (removed)
            {
                if (set.Count == 0)
                {
                    map.Remove(groupChatId);
                }

                await SaveAsync(map, ct);
            }
            return removed;
        }
        finally { Gate.Release(); }
    }

    public Task<Dictionary<long, HashSet<long>>> ListAsync(CancellationToken ct = default) => LoadAsync(ct);

    private async Task<Dictionary<long, HashSet<long>>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using FileStream s = File.OpenRead(_path);
            Dictionary<long, HashSet<long>>? map = await JsonSerializer.DeserializeAsync<Dictionary<long, HashSet<long>>>(s, cancellationToken: ct);
            return map ?? [];
        }
        catch { return []; }
    }

    private async Task SaveAsync(Dictionary<long, HashSet<long>> map, CancellationToken ct)
    {
        await using FileStream s = File.Open(_path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(s, map, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}

