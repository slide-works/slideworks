using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of PUT /api/decks/{id}/qa/settings (qa-control.json "settingsRequest", Q6) and IQaSettings.UpdateAsync's change:
// any of the five settings; a field left out (null) keeps its value.
public sealed record QaSettingsPatch(QaQuestions? Questions, bool? Review, bool? Names, bool? Reactions, QaShowOn? ShowOn);
