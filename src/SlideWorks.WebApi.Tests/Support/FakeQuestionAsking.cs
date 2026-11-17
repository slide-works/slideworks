using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IQuestionAsking for tests that need an ask without the real rules: each test sets the result a join code answers with, and
// every call records its code, guest and request, in order. Add it with FakeQuestionAsking.AddTo. A code with no result set
// throws, so a missing Set shows at once. The app, and so this fake, is shared by every test in a fixture: each test uses a
// code of its own, or calls Reset when it is done.
public sealed class FakeQuestionAsking : IQuestionAsking
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AskResult> _results = new(StringComparer.Ordinal);
    private readonly List<(string Code, string GuestId, AskRequest Request)> _calls = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeQuestionAsking>().AddSingleton<IQuestionAsking>(provider => provider.GetRequiredService<FakeQuestionAsking>());

    public void Set(string code, AskResult result)
    {
        lock (_gate)
        {
            _results[code] = result;
        }
    }

    // Every ask for this code, in order.
    public IReadOnlyList<(string GuestId, AskRequest Request)> CallsFor(string code)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Code == code).Select(c => (c.GuestId, c.Request))];
        }
    }

    // Forgets every result and call.
    public void Reset()
    {
        lock (_gate)
        {
            _results.Clear();
            _calls.Clear();
        }
    }

    public Task<AskResult> AskAsync(string code, string guestId, AskRequest request)
    {
        lock (_gate)
        {
            _calls.Add((code, guestId, request));
            return _results.TryGetValue(code, out var result)
                ? Task.FromResult(result)
                : throw new InvalidOperationException($"FakeQuestionAsking has no result for code {code}; call Set first.");
        }
    }
}
