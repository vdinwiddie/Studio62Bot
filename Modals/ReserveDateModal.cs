using Discord;
using Discord.Interactions;

namespace Studio62Bot.Modals;

public sealed class ReserveDateModal : IModal
{
    public string Title => "Reserve - Date";

    [InputLabel("Date (M-D)")]
    [ModalTextInput("date", TextInputStyle.Short, placeholder: "4-7", minLength: 3, maxLength: 5)]
    public string Date { get; set; } = string.Empty;
}
