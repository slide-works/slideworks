using System.Text.Json.Serialization;
using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Slides;

// add_slide: { slide, steps, revision, url, warnings } (K6, L7). slide is the new slide's number, revision the one
// update_slide needs, url the absolute player link to the slide, and warnings says when its layout is outside the deck's
// template (D9): the slide is kept and still renders. poll is the settings just stored on a poll slide (SW-568), and is
// left out on every other layout, so those keep the five keys above.
public sealed record AddSlideResult(
    int Slide,
    int Steps,
    long Revision,
    string Url,
    IReadOnlyList<string> Warnings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PollSettings? Poll = null);
