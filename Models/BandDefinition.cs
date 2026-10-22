namespace Studio62Bot.Models;

public sealed class BandDefinition
{
    public ulong RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
}
