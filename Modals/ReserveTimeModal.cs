using Discord;
using Discord.Interactions;

namespace Studio62Bot.Modals;

public sealed class ReserveTimeModal : IModal
{
    public string Title => "Reserve - Time";

    [InputLabel("Time (24-hour)")]
    [ModalTextInput("time", TextInputStyle.Short, placeholder: "18:30", minLength: 4, maxLength: 5)]
    public string Time { get; set; } = string.Empty;
}
