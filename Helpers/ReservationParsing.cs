using System.Globalization;

namespace Studio62Bot.Helpers;

public static class ReservationParsing
{
    public static bool TryParseFutureDate(string input, DateTime nowLocal, out DateOnly date, out string error)
    {
        error = string.Empty;
        date = default;

        if (!DateTime.TryParseExact(input.Trim(), ["M-d", "MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = "Date must be in M-D format, like `4-7` or `10-25`.";
            return false;
        }

        var currentYearCandidate = new DateTime(nowLocal.Year, parsed.Month, parsed.Day);
        var finalDate = currentYearCandidate.Date < nowLocal.Date
            ? currentYearCandidate.AddYears(1)
            : currentYearCandidate;

        if (finalDate.Date > nowLocal.Date.AddYears(1))
        {
            error = "Reservations can only be made up to one year ahead.";
            return false;
        }

        date = DateOnly.FromDateTime(finalDate);
        return true;
    }

    public static bool TryParseTime(string input, out TimeOnly time, out string error)
    {
        error = string.Empty;
        time = default;

        if (!DateTime.TryParseExact(input.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = "Time must be in 24-hour format, like `6:30` or `18:30`.";
            return false;
        }

        time = TimeOnly.FromDateTime(parsed);
        return true;
    }
}
