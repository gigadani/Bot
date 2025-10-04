using System.Text.Json;
using Bot.Models;
using Bot.Services;
using Xunit;

namespace Bot.Tests;

public class GuestRepositoryTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"BotTests_{Guid.NewGuid():N}.jsonl");

    [Fact]
    public async Task GetLatestForAsync_ReturnsLatestByUserId()
    {
        var path = TempFile();
        try
        {
            var repo = new GuestRepository(path);
            var rec1 = new GuestRecord(1, 100, DateTimeOffset.UtcNow.AddMinutes(-10), "en", "Alice One", null, "alice", null, "Active");
            var rec2 = new GuestRecord(1, 100, DateTimeOffset.UtcNow, "en", "Alice Two", "Bob Friend", "alice", null, "Active");
            await repo.AppendAsync(rec1);
            await repo.AppendAsync(rec2);

            var latest = await repo.GetLatestForAsync(100, 1);
            Assert.NotNull(latest);
            Assert.Equal("Alice Two", latest!.FullName);
            Assert.Equal("Bob Friend", latest.AvecFullName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetLatestForAsync_FallsBackToChatId()
    {
        var path = TempFile();
        try
        {
            var repo = new GuestRepository(path);
            var rec1 = new GuestRecord(9999, 0, DateTimeOffset.UtcNow.AddMinutes(-1), "fi", "Matti Meikalainen", null, null, null, "Active");
            await repo.AppendAsync(rec1);

            var latest = await repo.GetLatestForAsync(0, 9999);
            Assert.NotNull(latest);
            Assert.Equal("Matti Meikalainen", latest!.FullName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
