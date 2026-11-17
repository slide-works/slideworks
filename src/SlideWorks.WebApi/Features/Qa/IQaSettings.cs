using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// A deck's Q&A settings and its per-slide switches (Q1, Q6, D16), changeable live or not. A deck with no settings row has
// QaSettings.Default; a slide with no switch row has QaSlideSwitch.On. Each change records activity, then calls
// IAudienceRelay.RefreshAsync, then IQuestionPublisher.PublishAsync; questions leaving off while live calls
// IQaRounds.EnsureAsync. The routes answer QaControlView by reading IQuestionModeration.ControlAsync after the change: this
// seam never builds one, because moderation reads its settings here. Per request, because it uses the database.
public interface IQaSettings
{
    Task<QaSettings> GetAsync(string deckId);

    // Applies only the fields given and returns the settings after the change. The row is made on the first change.
    Task<QaSettings> UpdateAsync(string deckId, QaSettingsPatch patch);

    // Sets the switch of the 1-based slide, resolved at call time, and returns it after the change. An unknown slide throws
    // NotFoundException.
    Task<QaSlideSwitch> SetSlideAsync(string deckId, int slide, QaSlidePatch patch);
}
