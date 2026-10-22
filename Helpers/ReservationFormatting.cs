using Studio62Bot.Models;

namespace Studio62Bot.Helpers;

public static class ReservationFormatting
{
    public static string ToDisplayLine(Reservation reservation)
        => $"#{reservation.ReservationId} | {reservation.StartLocal:yyyy-MM-dd} | {reservation.StartLocal:HH:mm}-{reservation.EndLocal:HH:mm}";

    public static string ToDetailBlock(Reservation reservation)
        => $"Reservation ID: #{reservation.ReservationId}\nBand: {reservation.BandName}\nDate: {reservation.StartLocal:yyyy-MM-dd}\nTime: {reservation.StartLocal:HH:mm}-{reservation.EndLocal:HH:mm}\nDuration: {reservation.DurationHours} hour(s)";
}
