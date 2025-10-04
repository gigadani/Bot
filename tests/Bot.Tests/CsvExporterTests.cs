using System.Text;
using System.Text.Json;

using Bot.Models;
using Bot.Util;

using Xunit;

namespace Bot.Tests;

public class CsvExporterTests
{
    private static string TempFile(string ext) => Path.Combine(Path.GetTempPath(), $"BotCsv_{Guid.NewGuid():N}.{ext}");

    [Fact]
    public async Task Export_KeepsLatestActive_AndFlattens()
    {
        var input = TempFile("jsonl");
        var output = TempFile("csv");
        try
        {
            // Two records for same user; second is latest and Active
            var r1 = new GuestRecord(1, 100, DateTimeOffset.UtcNow.AddMinutes(-5), "en", "Alice One", null, "alice", null, "Active");
            var r2 = new GuestRecord(1, 100, DateTimeOffset.UtcNow, "en", "Alice Two", "Bob Friend", "alice", null, "Active");
            await File.WriteAllLinesAsync(input,
            [
                JsonSerializer.Serialize(r1),
                JsonSerializer.Serialize(r2)
            ], Encoding.UTF8);

            var code = await CsvExporter.ExportAsync(output, input);
            Assert.Equal(0, code);
            var lines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
            Assert.True(lines.Length >= 2, "CSV should have header + at least one row");
            var row = lines[1];
            Assert.Contains("Alice Two", row);
            Assert.Contains("Bob Friend", row);
        }
        finally
        {
            if (File.Exists(input))
            {
                File.Delete(input);
            }

            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }

    [Fact]
    public async Task Export_ExcludesUsersWithDeletedLatest()
    {
        var input = TempFile("jsonl");
        var output = TempFile("csv");
        try
        {
            var active = new GuestRecord(2, 200, DateTimeOffset.UtcNow.AddMinutes(-10), "fi", "Teppo Testaaja", null, "teppo", null, "Active");
            var deleted = new GuestRecord(2, 200, DateTimeOffset.UtcNow, "fi", "Teppo Testaaja", null, "teppo", null, "Deleted");
            await File.WriteAllLinesAsync(input,
            [
                JsonSerializer.Serialize(active),
                JsonSerializer.Serialize(deleted)
            ], Encoding.UTF8);

            var code = await CsvExporter.ExportAsync(output, input);
            Assert.Equal(0, code);
            var lines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
            // Only header expected when latest is Deleted
            Assert.Single(lines);
        }
        finally
        {
            if (File.Exists(input))
            {
                File.Delete(input);
            }

            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }
}
