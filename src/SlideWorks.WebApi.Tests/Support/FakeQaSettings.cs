using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IQaSettings for tests that need settings without the real rows: it keeps each deck's settings and each slide's switch in
// memory, starting from QaSettings.Default and QaSlideSwitch.On, applies a patch as the real one does (only the fields
// given) and records each change as "Update" or "SetSlide 3", per deck, in order. A test can also Set a deck's settings or
// a slide's switch outright, and FailWith makes every change throw (a NotFoundException for an unknown slide, say) and
// store nothing. Add it with FakeQaSettings.AddTo. The app, and so this fake, is shared by every test in a fixture: each
// test uses deck ids of its own, and a test that calls FailWith (which applies to every deck) calls Reset when it is done.
public sealed class FakeQaSettings : IQaSettings
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, QaSettings> _settings = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Deck, int Slide), QaSlideSwitch> _switches = [];
    private readonly List<(string Deck, string Call)> _calls = [];
    private Exception? _failure;

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeQaSettings>().AddSingleton<IQaSettings>(provider => provider.GetRequiredService<FakeQaSettings>());

    public void Set(string deckId, QaSettings settings)
    {
        lock (_gate)
        {
            _settings[deckId] = settings;
        }
    }

    public void SetSwitch(string deckId, int slide, QaSlideSwitch slideSwitch)
    {
        lock (_gate)
        {
            _switches[(deckId, slide)] = slideSwitch;
        }
    }

    // The switch of the 1-based slide as stored, QaSlideSwitch.On when none is.
    public QaSlideSwitch SwitchOf(string deckId, int slide)
    {
        lock (_gate)
        {
            return _switches.GetValueOrDefault((deckId, slide), QaSlideSwitch.On);
        }
    }

    // Every change from now on throws this and stores nothing; null accepts them again. GetAsync still answers.
    public void FailWith(Exception? failure)
    {
        lock (_gate)
        {
            _failure = failure;
        }
    }

    public IReadOnlyList<string> CallsFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Deck == deckId).Select(c => c.Call)];
        }
    }

    // Forgets every deck's settings, every switch, every call and the failure.
    public void Reset()
    {
        lock (_gate)
        {
            _settings.Clear();
            _switches.Clear();
            _calls.Clear();
            _failure = null;
        }
    }

    public Task<QaSettings> GetAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(_settings.GetValueOrDefault(deckId, QaSettings.Default));
        }
    }

    public Task<QaSettings> UpdateAsync(string deckId, QaSettingsPatch patch)
    {
        lock (_gate)
        {
            ThrowIfFailing();
            var now = _settings.GetValueOrDefault(deckId, QaSettings.Default);
            var next = new QaSettings(
                patch.Questions ?? now.Questions,
                patch.Review ?? now.Review,
                patch.Names ?? now.Names,
                patch.Reactions ?? now.Reactions,
                patch.ShowOn ?? now.ShowOn);
            _settings[deckId] = next;
            _calls.Add((deckId, "Update"));
            return Task.FromResult(next);
        }
    }

    public Task<QaSlideSwitch> SetSlideAsync(string deckId, int slide, QaSlidePatch patch)
    {
        lock (_gate)
        {
            ThrowIfFailing();
            var now = _switches.GetValueOrDefault((deckId, slide), QaSlideSwitch.On);
            var next = new QaSlideSwitch(patch.Questions ?? now.Questions, patch.Reactions ?? now.Reactions);
            _switches[(deckId, slide)] = next;
            _calls.Add((deckId, $"SetSlide {slide}"));
            return Task.FromResult(next);
        }
    }

    private void ThrowIfFailing()
    {
        if (_failure != null)
        {
            throw _failure;
        }
    }
}
