using System.Text.Json.Serialization;

namespace Bot.Models;

public sealed record GuestRecord(
    long ChatId,
    long UserId,
    DateTimeOffset Timestamp,
    string Language,
    string FullName,
    string? AvecFullName,
    string? TelegramUsername,
    string? AvecUsername,
    string? Status
);
