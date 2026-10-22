using Studio62Bot.Models;
using System.Text.Json;

namespace Studio62Bot.Services;

public sealed class BandConfigService
{
    private readonly object _lock = new();
    private IReadOnlyList<BandDefinition> _cache = Array.Empty<BandDefinition>();
    private DateTime _lastReadUtc = DateTime.MinValue;

    public IReadOnlyList<BandDefinition> GetBands()
    {
        lock (_lock)
        {
            var path = FindBandsPath();
            if (path is null)
            {
                return Array.Empty<BandDefinition>();
            }

            var lastWrite = File.GetLastWriteTimeUtc(path);
            if (lastWrite <= _lastReadUtc && _cache.Count > 0)
            {
                return _cache;
            }

            var json = File.ReadAllText(path);
            var bands = JsonSerializer.Deserialize<List<BandDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<BandDefinition>();

            _cache = bands
                .Where(x => x.Enabled && x.RoleId != 0 && !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _lastReadUtc = lastWrite;
            return _cache;
        }
    }

    public BandDefinition? GetBandByRoleId(ulong roleId) => GetBands().FirstOrDefault(x => x.RoleId == roleId);

    public IReadOnlyList<BandDefinition> GetBandsForUser(IReadOnlyCollection<ulong> userRoleIds)
    {
        var lookup = new HashSet<ulong>(userRoleIds);
        return GetBands().Where(x => lookup.Contains(x.RoleId)).ToList();
    }

    private static string? FindBandsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "bands.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
