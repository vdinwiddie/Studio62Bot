namespace Studio62Bot.Models;

public sealed class Reservation
{
    public long ReservationId { get; set; }
    public ulong BandRoleId { get; set; }
    public string BandName { get; set; } = string.Empty;
    public ulong ReservedByUserId { get; set; }
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }
    public DateTime CreatedUtc { get; set; }

    public int DurationHours => (int)Math.Round((EndLocal - StartLocal).TotalHours);
}
