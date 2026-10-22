namespace Studio62Bot.Configuration;

public sealed class BotSettings
{
    public ulong? RegisterCommandsToGuildId { get; set; }
    public string DatabasePath { get; set; } = "studio62bot.db";
}
