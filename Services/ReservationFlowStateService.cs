using Studio62Bot.Models;
using System.Collections.Concurrent;

namespace Studio62Bot.Services;

public sealed class ReservationFlowStateService
{
    private readonly ConcurrentDictionary<string, ReserveFlowState> _flows = new();

    public ReserveFlowState Start(ulong userId, BandDefinition band)
    {
        var state = new ReserveFlowState
        {
            FlowId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            BandRoleId = band.RoleId,
            BandName = band.Name,
            CreatedUtc = DateTime.UtcNow
        };

        _flows[state.FlowId] = state;
        CleanupExpired();
        return state;
    }

    public ReserveFlowState? Get(string flowId)
    {
        if (_flows.TryGetValue(flowId, out var state))
        {
            if (DateTime.UtcNow - state.CreatedUtc <= TimeSpan.FromMinutes(15))
            {
                return state;
            }

            _flows.TryRemove(flowId, out _);
        }

        return null;
    }

    public void Complete(string flowId) => _flows.TryRemove(flowId, out _);

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        foreach (var pair in _flows)
        {
            if (pair.Value.CreatedUtc < cutoff)
            {
                _flows.TryRemove(pair.Key, out _);
            }
        }
    }
}

public sealed class ReserveFlowState
{
    public string FlowId { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public ulong BandRoleId { get; set; }
    public string BandName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
}
