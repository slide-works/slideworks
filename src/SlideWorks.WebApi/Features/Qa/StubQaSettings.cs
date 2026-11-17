using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real settings: the deck's settings row, the slide switch rows, activity, the refresh and the publisher.
// Every deck has the defaults and every slide both switches on; a change is applied to them and returned, never stored.
[RegisterPerRequest(For = typeof(IQaSettings))]
public sealed class StubQaSettings : IQaSettings
{
    public Task<QaSettings> GetAsync(string deckId) => Task.FromResult(QaSettings.Default);

    public Task<QaSettings> UpdateAsync(string deckId, QaSettingsPatch patch)
        => Task.FromResult(new QaSettings(
            patch.Questions ?? QaSettings.Default.Questions,
            patch.Review ?? QaSettings.Default.Review,
            patch.Names ?? QaSettings.Default.Names,
            patch.Reactions ?? QaSettings.Default.Reactions,
            patch.ShowOn ?? QaSettings.Default.ShowOn));

    public Task<QaSlideSwitch> SetSlideAsync(string deckId, int slide, QaSlidePatch patch)
        => Task.FromResult(new QaSlideSwitch(patch.Questions ?? QaSlideSwitch.On.Questions, patch.Reactions ?? QaSlideSwitch.On.Reactions));
}
