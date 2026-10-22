using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Studio62Bot.Helpers;
using Studio62Bot.Modals;
using Studio62Bot.Services;
using System.Text;

namespace Studio62Bot.Commands;

public sealed class ReserveModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BandConfigService _bandConfig;
    private readonly ReservationFlowStateService _flowState;
    private readonly ReservationRepository _repository;

    public ReserveModule(BandConfigService bandConfig, ReservationFlowStateService flowState, ReservationRepository repository)
    {
        _bandConfig = bandConfig;
        _flowState = flowState;
        _repository = repository;
    }

    [SlashCommand("reserve", "Create a reservation through a guided flow.")]
    public async Task ReserveAsync()
    {
        if (Context.User is not SocketGuildUser guildUser)
        {
            await RespondAsync("This command only works inside a server.", ephemeral: true);
            return;
        }

        var userRoleIds = guildUser.Roles.Select(r => r.Id).ToArray();
        var userBands = _bandConfig.GetBandsForUser(userRoleIds);
        if (userBands.Count == 0)
        {
            await RespondAsync("You do not currently have any configured band roles, so there are no bands available to reserve under.", ephemeral: true);
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId($"reserve-band:{Context.User.Id}")
            .WithPlaceholder("Choose the band for this reservation")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var band in userBands.Take(25))
        {
            menu.AddOption(band.Name, band.RoleId.ToString());
        }

        var components = new ComponentBuilder().WithSelectMenu(menu).Build();
        await RespondAsync("Step 1 of 4: choose the band for this reservation.", components: components, ephemeral: true);
    }

    [ComponentInteraction("reserve-band:*")]
    public async Task HandleBandSelectionAsync(string expectedUserId)
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

        if (!guildUser.Roles.Any(r => r.Id == roleId))
        {
            await RespondAsync("You can only reserve time for a band whose role you currently have.", ephemeral: true);
            return;
        }

        var band = _bandConfig.GetBandByRoleId(roleId);
        if (band is null)
        {
            await RespondAsync("That band is no longer configured.", ephemeral: true);
            return;
        }

        var flow = _flowState.Start(Context.User.Id, band);
        await RespondWithModalAsync<ReserveDateModal>($"reserve-date:{flow.FlowId}");
    }

    [ModalInteraction("reserve-date:*")]
    public async Task HandleDateModalAsync(string flowId, ReserveDateModal modal)
    {
        var flow = _flowState.Get(flowId);
        if (flow is null || flow.UserId != Context.User.Id)
        {
            await RespondAsync("That reservation flow expired. Please run `/reserve` again.", ephemeral: true);
            return;
        }

        if (!ReservationParsing.TryParseFutureDate(modal.Date, DateTime.Now, out var date, out var error))
        {
            _flowState.Complete(flowId);
            await RespondAsync(error, ephemeral: true);
            return;
        }

        flow.Date = date;

        var continueButton = new ComponentBuilder()
            .WithButton("Continue to time entry", $"reserve-open-time:{flowId}", ButtonStyle.Primary)
            .Build();

        await RespondAsync($"Date saved: {date:yyyy-MM-dd}. Click below to enter the start time.", components: continueButton, ephemeral: true);
    }

    [ComponentInteraction("reserve-open-time:*")]
    public async Task OpenTimeModalAsync(string flowId)
    {
        var flow = _flowState.Get(flowId);
        if (flow is null || flow.UserId != Context.User.Id || flow.Date is null)
        {
            await RespondAsync("That reservation flow expired. Please run `/reserve` again.", ephemeral: true);
            return;
        }

        await RespondWithModalAsync<ReserveTimeModal>($"reserve-time:{flowId}");
    }

    [ModalInteraction("reserve-time:*")]
    public async Task HandleTimeModalAsync(string flowId, ReserveTimeModal modal)
    {
        var flow = _flowState.Get(flowId);
        if (flow is null || flow.UserId != Context.User.Id || flow.Date is null)
        {
            await RespondAsync("That reservation flow expired. Please run `/reserve` again.", ephemeral: true);
            return;
        }

        if (!ReservationParsing.TryParseTime(modal.Time, out var time, out var error))
        {
            _flowState.Complete(flowId);
            await RespondAsync(error, ephemeral: true);
            return;
        }

        flow.Time = time;

        var durationMenu = new SelectMenuBuilder()
            .WithCustomId($"reserve-duration:{flowId}")
            .WithPlaceholder("Choose duration in hours")
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption("1 hour", "1")
            .AddOption("2 hours", "2")
            .AddOption("3 hours", "3")
            .AddOption("4 hours", "4");

        var components = new ComponentBuilder().WithSelectMenu(durationMenu).Build();
        await RespondAsync("Step 4 of 4: choose the reservation duration.", components: components, ephemeral: true);
    }

    [ComponentInteraction("reserve-duration:*")]
    public async Task HandleDurationSelectionAsync(string flowId)
    {
        var flow = _flowState.Get(flowId);
        if (flow is null || flow.UserId != Context.User.Id || flow.Date is null || flow.Time is null)
        {
            await RespondAsync("That reservation flow expired. Please run `/reserve` again.", ephemeral: true);
            return;
        }

        var selectedValue = ((SocketMessageComponent)Context.Interaction).Data.Values.FirstOrDefault();
        if (!int.TryParse(selectedValue, out var durationHours) || durationHours < 1 || durationHours > 4)
        {
            _flowState.Complete(flowId);
            await RespondAsync("Duration must be 1, 2, 3, or 4 hours.", ephemeral: true);
            return;
        }

        var startLocal = flow.Date.Value.ToDateTime(flow.Time.Value);
        var endLocal = startLocal.AddHours(durationHours);

        if (startLocal < DateTime.Now)
        {
            _flowState.Complete(flowId);
            await UpdateComponentMessageAsync("Reservations cannot start in the past.");
            return;
        }

        if (endLocal.Date != startLocal.Date)
        {
            _flowState.Complete(flowId);
            await UpdateComponentMessageAsync("Reservations cannot cross midnight. Choose an earlier start time or shorter duration.");
            return;
        }

        if (startLocal.Date > DateTime.Now.Date.AddYears(1))
        {
            _flowState.Complete(flowId);
            await UpdateComponentMessageAsync("Reservations can only be made up to one year ahead.");
            return;
        }

        var conflict = await _repository.FindConflictAsync(startLocal, endLocal);
        if (conflict is not null)
        {
            _flowState.Complete(flowId);
            await UpdateComponentMessageAsync(new StringBuilder()
                .AppendLine("That time conflicts with an existing reservation:")
                .AppendLine($"Reservation ID: #{conflict.ReservationId}")
                .AppendLine($"Band: {conflict.BandName}")
                .AppendLine($"Date: {conflict.StartLocal:yyyy-MM-dd}")
                .AppendLine($"Time: {conflict.StartLocal:HH:mm}-{conflict.EndLocal:HH:mm}")
                .ToString());
            return;
        }

        var reservation = await _repository.CreateAsync(flow.BandRoleId, flow.BandName, Context.User.Id, startLocal, endLocal);
        _flowState.Complete(flowId);

        var confirmation = new StringBuilder()
            .AppendLine("Reservation created successfully.")
            .AppendLine($"Reservation ID: #{reservation.ReservationId}")
            .AppendLine($"Band: {reservation.BandName}")
            .AppendLine($"Date: {reservation.StartLocal:yyyy-MM-dd}")
            .AppendLine($"Time: {reservation.StartLocal:HH:mm}-{reservation.EndLocal:HH:mm}")
            .AppendLine($"Duration: {reservation.DurationHours} hour(s)")
            .ToString();

        await UpdateComponentMessageAndSendPublicAsync(confirmation, confirmation);
    }

    private async Task UpdateComponentMessageAsync(string content)
    {
        if (Context.Interaction is SocketMessageComponent component)
        {
            await component.UpdateAsync(message =>
            {
                message.Content = content;
                message.Components = new ComponentBuilder().Build();
            });
            return;
        }

        await RespondAsync(content, ephemeral: true);
    }

    private async Task UpdateComponentMessageAndSendPublicAsync(string hiddenContent, string publicContent)
    {
        await UpdateComponentMessageAsync(hiddenContent);

        if (Context.Channel is not null)
        {
            await Context.Channel.SendMessageAsync(publicContent);
        }
    }
}
