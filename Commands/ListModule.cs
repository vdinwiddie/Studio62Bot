using Discord.Interactions;
using Discord.WebSocket;
using Studio62Bot.Helpers;
using Studio62Bot.Services;
using System.Text;

namespace Studio62Bot.Commands;

public sealed class ListModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ReservationRepository _repository;
    private readonly BandConfigService _bandConfig;

    public ListModule(ReservationRepository repository, BandConfigService bandConfig)
    {
        _repository = repository;
        _bandConfig = bandConfig;
    }

    [SlashCommand("list", "List all upcoming reservations grouped by band.")]
    public async Task ListAsync()
    {
        var reservations = await _repository.GetUpcomingAsync(DateTime.Now);
        if (reservations.Count == 0)
        {
            await RespondAsync("There are no upcoming reservations.", ephemeral: false);
            return;
        }

        var configuredBandOrder = _bandConfig.GetBands()
            .Select((band, index) => new { band.Name, Index = index })
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var grouped = reservations
            .GroupBy(x => x.BandName)
            .OrderBy(g => configuredBandOrder.TryGetValue(g.Key, out var index) ? index : int.MaxValue)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder();
        foreach (var bandGroup in grouped)
        {
            builder.AppendLine($"**{bandGroup.Key}**");

            foreach (var reservation in bandGroup.OrderBy(x => x.StartLocal))
            {
                builder.AppendLine(ReservationFormatting.ToDisplayLine(reservation));
            }

            builder.AppendLine();
        }

        var content = builder.ToString().TrimEnd();
        if (content.Length <= 2000)
        {
            await RespondAsync(content, ephemeral: false);
            return;
        }

        await RespondAsync("There are too many reservations to display in one Discord message right now. Consider pruning old future entries or splitting the list command later.", ephemeral: false);
    }
}
