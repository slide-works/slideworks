using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The submit limit on word-cloud and open-text slides (SW-8): 30 submits a minute per guest per slide, then 429
// too_many_answers. The key is the guest id, so a room behind one NAT does not share it. The window slides: a submit
// counts for one minute after it is taken.
[RegisterSingleton]
public sealed class OpenResponseLimiter(TimeProvider clock)
{
    public const string TOO_MANY_ANSWERS = "too_many_answers";
    public const int LIMIT = 30;
    public static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly Dictionary<(string Guest, string Code, int Slide), Queue<long>> _taken = [];
    private readonly Lock _lock = new();

    // Counts one submit, or refuses it with 429 too_many_answers when the guest already sent LIMIT to this slide in the window.
    public void Take(string guestId, string code, int slide)
    {
        var now = clock.GetTimestamp();
        lock (_lock)
        {
            PruneExpired(now);
            var key = (guestId, code, slide);
            if (!_taken.TryGetValue(key, out var taken))
            {
                taken = new Queue<long>();
                _taken[key] = taken;
            }
            if (taken.Count >= LIMIT)
            {
                throw new TooManyRequestsException(TOO_MANY_ANSWERS);
            }
            taken.Enqueue(now);
        }
    }

    private void PruneExpired(long now)
    {
        foreach (var (key, taken) in _taken)
        {
            while (taken.Count > 0 && clock.GetElapsedTime(taken.Peek(), now) >= WINDOW)
            {
                taken.Dequeue();
            }
            if (taken.Count == 0)
            {
                _taken.Remove(key);
            }
        }
    }
}
