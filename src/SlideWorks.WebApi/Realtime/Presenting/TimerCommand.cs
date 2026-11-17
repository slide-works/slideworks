namespace SlideWorks.WebApi.Realtime.Presenting;

// What SetTimer asks of the timer (P2). On the wire as "pause", "resume" or "reset".
public enum TimerCommand
{
    Pause,
    Resume,
    Reset,
}
