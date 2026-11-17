using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Data;

// How many times one emoji was tapped while one slide was on screen in one session (Q7, D19), keyed by all three. Written
// every 5 seconds and when the session ends by adding what was counted in memory; never a row per tap. Count is never
// negative (a check constraint). Never deleted. Only Features/Qa writes it.
public class ReactionTotal
{
    public Guid SessionId { get; set; }

    public Guid SlideId { get; set; }

    public ReactionEmoji Emoji { get; set; }

    public int Count { get; set; }
}
