using System.Collections.Concurrent;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.DeviceSessions;

namespace SlideWorks.WebApi.Realtime;

// The open deck-hub connections of each device session (K5). DeckHub adds a connection a device bearer opened and
// removes it when it closes; when a session ends, IDeviceSessions.EndAsync tells this observer and every connection of
// that session is aborted at once. It never throws: EndAsync stops at the first observer that does, so a failed abort is
// logged and the rest still run.
[RegisterSingleton(Concrete = true)]
[RegisterSingleton(For = typeof(IDeviceSessionEndedObserver), Factory = typeof(Seam))]
public sealed partial class DeviceConnections(ILogger<DeviceConnections> logger) : IDeviceSessionEndedObserver
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HubCallerContext>> _bySession = new();

    public void Add(Guid sessionId, HubCallerContext connection)
        => _bySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, HubCallerContext>())[connection.ConnectionId] = connection;

    public void Remove(Guid sessionId, string connectionId)
    {
        if (_bySession.TryGetValue(sessionId, out var connections) && connections.TryRemove(connectionId, out _) && connections.IsEmpty)
        {
            _bySession.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<string, HubCallerContext>>(sessionId, connections));
        }
    }

    public Task OnEndedAsync(Guid sessionId)
    {
        try
        {
            if (_bySession.TryRemove(sessionId, out var connections))
            {
                foreach (var connection in connections.Values)
                {
                    Abort(sessionId, connection);
                }
            }
        }
        catch (Exception e)
        {
            LogAbortFailed(e, sessionId);
        }
        return Task.CompletedTask;
    }

    private void Abort(Guid sessionId, HubCallerContext connection)
    {
        try
        {
            connection.Abort();
        }
        catch (Exception e)
        {
            LogAbortFailed(e, sessionId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping the hub connections of device session {SessionId} failed")]
    private partial void LogAbortFailed(Exception exception, Guid sessionId);

    public sealed class Seam : IComponentFactory<IDeviceSessionEndedObserver>
    {
        public IDeviceSessionEndedObserver Build(IComponentResolver c) => c.Resolve<DeviceConnections>();
    }
}
