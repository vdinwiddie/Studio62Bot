using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Studio62Bot.Configuration;
using Studio62Bot.Services;
using System.Reflection;
using System.Text.Json;

namespace Studio62Bot;

public sealed class Program
{
    private DiscordSocketClient _client = null!;
    private InteractionService _interactions = null!;
    private IServiceProvider _services = null!;
    private BotSettings _settings = null!;
    private bool _commandsRegistered;

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        _settings = LoadSettings();

        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = false,
            LogGatewayIntentWarnings = true
        };

        _client = new DiscordSocketClient(socketConfig);
        _interactions = new InteractionService(_client.Rest);

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_interactions)
            .AddSingleton(_settings)
            .AddSingleton<BandConfigService>()
            .AddSingleton<ReservationRepository>()
            .AddSingleton<ReservationFlowStateService>()
            .BuildServiceProvider();

        _client.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        var repository = _services.GetRequiredService<ReservationRepository>();
        await repository.InitializeAsync();

        var token = ReadBotToken();

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        await Task.Delay(Timeout.Infinite);
    }

    private async Task ReadyAsync()
    {
        if (_commandsRegistered)
        {
            return;
        }

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        if (_settings.RegisterCommandsToGuildId is ulong guildId && guildId != 0)
        {
            await _interactions.RegisterCommandsToGuildAsync(guildId, deleteMissing: true);
            Console.WriteLine($"Slash commands registered to guild {guildId}.");
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync(deleteMissing: true);
            Console.WriteLine("Slash commands registered globally.");
        }

        _commandsRegistered = true;
        Console.WriteLine($"{_client.CurrentUser} is connected.");
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess && interaction.Type is not InteractionType.ApplicationCommandAutocomplete)
            {
                Console.WriteLine($"Interaction error: {result.Error}: {result.ErrorReason}");

                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync("Something went wrong while handling that interaction.", ephemeral: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            if (!interaction.HasResponded)
            {
                await interaction.RespondAsync("Unexpected bot error.", ephemeral: true);
            }
        }
    }

    private static BotSettings LoadSettings()
    {
        var settingsPath = FindFileUpwards("botsettings.json");
        if (settingsPath is null)
        {
            return new BotSettings();
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<BotSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new BotSettings();
    }

    private static string ReadBotToken()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "botsettings.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Could not find config file at {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<BotConfig>(json);

        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
        {
            throw new InvalidOperationException("BotToken is missing or empty in botsettings.json");
        }

        return config.BotToken.Trim();
    }

    private static string? FindFileUpwards(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static Task LogAsync(LogMessage message)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{message.Severity}] {message.Source}: {message.Message} {message.Exception}");
        return Task.CompletedTask;
    }
}

public class BotConfig
{
    public string BotToken { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public string DatabasePath { get; set; } = string.Empty;
}
