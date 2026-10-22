using Discord.Interactions;
using Discord.WebSocket;

namespace Studio62Bot.Commands;

public sealed class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with pong.")]
    public Task PingAsync() => RespondAsync("Pong!", ephemeral: true);
}
