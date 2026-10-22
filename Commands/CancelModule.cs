using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Studio62Bot.Helpers;
using Studio62Bot.Modals;
using Studio62Bot.Models;
using Studio62Bot.Services;

namespace Studio62Bot.Commands;

public sealed class CancelModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string AdminRoleName = "Admin";
    private readonly ReservationRepository _repository;

    public CancelModule(ReservationRepository repository)
    {
        _repository = repository;
    }

    [SlashCommand("cancel", "Cancel a reservation by reservation ID.")]
    public Task CancelAsync() => RespondWithModalAsync<CancelReservationModal>($"cancel-reservation:{Context.User.Id}");

    [ModalInteraction("cancel-reservation:*")]
    public async Task HandleCancelModalAsync(string expectedUserId, CancelReservationModal modal)
    {
        if (!ulong.TryParse(expectedUserId, out var userId) || userId != Context.User.Id)
        {
            await RespondAsync("That cancel prompt is only for the user who opened it.", ephemeral: true);
            return;
        }

        if (Context.User is not SocketGuildUser guildUser)
        {
            await RespondAsync("This command only works inside a server.", ephemeral: true);
            return;
        }

        if (!long.TryParse(modal.ReservationId.Trim().TrimStart('#'), out var reservationId) || reservationId <= 0)
        {
            await RespondAsync("Reservation ID must be a valid number, like `123`.", ephemeral: true);
            return;
        }

        var reservation = await _repository.GetByIdAsync(reservationId);
        if (reservation is null)
        {
            await RespondAsync($"No reservation was found with ID #{reservationId}.", ephemeral: true);
            return;
        }

        if (!CanCancelReservation(guildUser, reservation))
        {
            await RespondAsync("You do not belong to the band associated with that reservation and do not have the Admin role, so you cannot cancel it.", ephemeral: true);
            return;
        }

        var buttons = new ComponentBuilder()
            .WithButton("Yes", $"cancel-confirm:{reservation.ReservationId}:{Context.User.Id}", ButtonStyle.Danger)
            .WithButton("No", $"cancel-decline:{reservation.ReservationId}:{Context.User.Id}", ButtonStyle.Secondary)
            .Build();

        await RespondAsync($"Cancel this reservation?\n\n{ReservationFormatting.ToDetailBlock(reservation)}", components: buttons, ephemeral: true);
    }

    [ComponentInteraction("cancel-confirm:*:*")]
    public async Task ConfirmCancelAsync(string reservationIdRaw, string userIdRaw)
    {
        if (!ValidateUser(userIdRaw, out _) || !long.TryParse(reservationIdRaw, out var reservationId))
        {
            await RespondAsync("That cancel action is invalid.", ephemeral: true);
            return;
        }

        if (Context.User is not SocketGuildUser guildUser)
        {
            await RespondAsync("This command only works inside a server.", ephemeral: true);
            return;
        }

        var reservation = await _repository.GetByIdAsync(reservationId);
        if (reservation is null)
        {
            await UpdateComponentMessageAsync($"Reservation #{reservationId} no longer exists.");
            return;
        }

        if (!CanCancelReservation(guildUser, reservation))
        {
            await UpdateComponentMessageAsync("You no longer have the required band role or Admin role, so this reservation cannot be canceled from this prompt.");
            return;
        }

        var deleted = await _repository.DeleteAsync(reservationId);
        var confirmation = deleted
            ? $"Reservation #{reservationId} was successfully canceled."
            : $"Reservation #{reservationId} could not be canceled because it no longer exists.";

        await UpdateComponentMessageAndSendPublicAsync(confirmation, confirmation);
    }

    [ComponentInteraction("cancel-decline:*:*")]
    public async Task DeclineCancelAsync(string reservationIdRaw, string userIdRaw)
    {
        if (!ValidateUser(userIdRaw, out _))
        {
            await RespondAsync("That cancel action is only for the user who opened it.", ephemeral: true);
            return;
        }

        await UpdateComponentMessageAsync($"Cancellation aborted for reservation #{reservationIdRaw}.");
    }

    private bool ValidateUser(string userIdRaw, out ulong parsedUserId)
    {
        parsedUserId = 0;
        return ulong.TryParse(userIdRaw, out parsedUserId) && parsedUserId == Context.User.Id;
    }

    private static bool CanCancelReservation(SocketGuildUser guildUser, Reservation reservation)
        => HasBandRole(guildUser, reservation) || HasAdminRole(guildUser);

    private static bool HasBandRole(SocketGuildUser guildUser, Reservation reservation)
        => guildUser.Roles.Any(role => role.Id == reservation.BandRoleId);

    private static bool HasAdminRole(SocketGuildUser guildUser)
        => guildUser.Roles.Any(role => string.Equals(role.Name, AdminRoleName, StringComparison.OrdinalIgnoreCase));

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
