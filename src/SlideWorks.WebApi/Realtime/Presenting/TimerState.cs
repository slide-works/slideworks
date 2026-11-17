namespace SlideWorks.WebApi.Realtime.Presenting;

// The talk timer's state (P1). On the wire as "notStarted", "running" or "paused".
public enum TimerState
{
    NotStarted,
    Running,
    Paused,
}
