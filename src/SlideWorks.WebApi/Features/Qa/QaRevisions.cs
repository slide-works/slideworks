using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// Each Q&A round's list revision (Q5): it moves whenever a question is added or changes state, hidden or on screen, and a
// phone re-reads its list when it does. Every revision is a QaSeq value, so it only increases, also after a restart: a round
// this instance doesn't hold yet starts from QaSeq.Next(). Held in memory, one entry per round for the life of the process.
[RegisterSingleton]
public sealed class QaRevisions(QaSeq seq)
{
    private readonly ConcurrentDictionary<string, long> _revisions = new(StringComparer.Ordinal);

    public long Current(string roundId) => _revisions.GetOrAdd(roundId, _ => seq.Next());

    // Moves the round's revision on and returns it.
    public long Bump(string roundId) => _revisions.AddOrUpdate(roundId, _ => seq.Next(), (_, _) => seq.Next());
}
