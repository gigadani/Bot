namespace Bot.Services;

public enum Step
{
    None = 0,
    AskLanguage,
    AskAction,
    AskFullName,
    AskPlusOne,
    AskAvecName,
    AskAvecHandle,
    ChangeAvecName,
    ChangeAvecHandle,
    Completed
}

public sealed class Session
{
    public long ChatId { get; init; }
    public Step Step { get; set; } = Step.AskLanguage;
    public long? UserId { get; set; }
    public string? Username { get; set; }
    public string? Language { get; set; }
    public string? FullName { get; set; }
    public bool WantsPlusOne { get; set; }
    public string? AvecFullName { get; set; }
    public string? AvecUsername { get; set; }
}
