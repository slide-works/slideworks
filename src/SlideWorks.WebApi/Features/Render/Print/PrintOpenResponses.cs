using SlideWorks.WebApi.Features.OpenResponses.Contracts;

namespace SlideWorks.WebApi.Features.Render.Print;

// The word-cloud and open-text results of the newest session: the records the big screen already gets for a round, with
// hidden entries left out.
public record PrintOpenResponses(IReadOnlyList<WordCloudTally> WordClouds, IReadOnlyList<OpenTextTally> OpenTexts);
