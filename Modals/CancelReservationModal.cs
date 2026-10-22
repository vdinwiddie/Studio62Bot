using Discord;
using Discord.Interactions;

namespace Studio62Bot.Modals;

public sealed class CancelReservationModal : IModal
{
    public string Title => "Cancel Reservation";

    [InputLabel("Reservation ID")]
    [ModalTextInput("reservation_id", TextInputStyle.Short, placeholder: "123", minLength: 1, maxLength: 20)]
    public string ReservationId { get; set; } = string.Empty;
}
