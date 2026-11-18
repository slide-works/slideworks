using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Mcp;

// What update_qa answers: the settings after the change as one sentence, and as fields. A deck change fills Settings; a
// slide change fills Slide and Switch (the slide's two switches, true for on). The other pair is null.
public sealed record UpdateQaResult(string Text, QaSettings? Settings, int? Slide, QaSlideSwitch? Switch);
