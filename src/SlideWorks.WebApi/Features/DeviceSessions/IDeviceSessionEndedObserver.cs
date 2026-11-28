namespace SlideWorks.WebApi.Features.DeviceSessions;

// Told when a device session ends (K5), after the end is committed. IDeviceSessions.EndAsync calls every registered one;
// the realtime side implements one that drops the session's open hub connections. No IHubContext under Features/.
public interface IDeviceSessionEndedObserver
{
    Task OnEndedAsync(Guid sessionId);
}
