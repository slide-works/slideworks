namespace SlideWorks.LoadTest;

// One phone in a run: it joins, then acts once inside the window.
public interface ILoadGuest : IAsyncDisposable
{
    bool Joined { get; }

    // False, with the error counted, when any step is refused.
    Task<bool> JoinAsync(CancellationToken ct);

    // Call it only after JoinAsync returned true. `remaining` is what is left of the window when the guest starts acting, so a
    // guest that spreads its actions out stays inside it.
    Task ActAsync(TimeSpan remaining, CancellationToken ct);
}
