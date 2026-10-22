using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Studio62Bot.Services;

namespace Studio62Bot.Commands;

public sealed class GroupModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BandConfigService _bandConfig;

    public GroupModule(BandConfigService bandConfig)
    {
        _bandConfig = bandConfig;
    }

    [SlashCommand("join-group", "Choose one of the configured band roles and assign it to yourself.")]
    public async Task JoinGroupAsync()
    {
        var bands = _bandConfig.GetBands();
        if (bands.Count == 0)
        {
            await RespondAsync("No bands are configured yet. Update `bands.json` first.", ephemeral: true);
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId($"join-group:{Context.User.Id}")
            .WithPlaceholder("Choose a band")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var band in bands.Take(25))
        {
            menu.AddOption(band.Name, band.RoleId.ToString());
        }

        var components = new ComponentBuilder().WithSelectMenu(menu).Build();
        await RespondAsync("Select the band role you want to add.", components: components, ephemeral: true);
    }

    [ComponentInteraction("join-group:*")]
    public async Task HandleJoinGroupSelectionAsync(string expectedUserId)
    {
        if (!ulong.TryParse(expectedUserId, out var userId) || userId != Context.User.Id)
        {
            await RespondAsync("That menu is only for the user who opened it.", ephemeral: true);
            return;
        }

        if (Context.User is not SocketGuildUser guildUser)
        {
            await RespondAsync("This command only works inside a server.", ephemeral: true);
            return;
        }

        var selectedValue = ((SocketMessageComponent)Context.Interaction).Data.Values.FirstOrDefault();
        if (!ulong.TryParse(selectedValue, out var roleId))
        {
            await RespondAsync("Invalid band selection.", ephemeral: true);
            return;
        }

        var band = _bandConfig.GetBandByRoleId(roleId);
        if (band is null)
        {
            await UpdateComponentMessageAsync("That band is no longer configured in `bands.json`.");
            return;
        }

        var role = guildUser.Guild.GetRole(roleId);
        if (role is null)
        {
            await UpdateComponentMessageAsync($"The Discord role for **{band.Name}** was not found. Check the role ID in `bands.json`.");
            return;
        }

        var alreadyHadRole = guildUser.Roles.Any(r => r.Id == roleId);
        if (!alreadyHadRole)
        {
            await guildUser.AddRoleAsync(role);
        }

        await UpdateComponentMessageAsync(
            alreadyHadRole
                ? $"You already had the **{band.Name}** role."
                : $"Successfully added the **{band.Name}** role.");
    }

    private Task UpdateComponentMessageAsync(string content)
    {
        if (Context.Interaction is SocketMessageComponent component)
        {
            return component.UpdateAsync(message =>
            {
                message.Content = content;
                message.Components = new ComponentBuilder().Build();
            });
        }

        return RespondAsync(content, ephemeral: true);
    }
}
