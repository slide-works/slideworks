using System.Text.Json;
using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// What an edit of a poll slide must keep true (V1, D13) and who must hear about it, shared by update_slide (SlideEdits) and
// the poll tools (PollSettingsCommands). While a round is open on the slide, its option count can't shrink and its order
// can't change, because answers are known by position; text may be fixed and options appended. A settings row must stay
// valid for the options the slide ends up with, so correct and max never point at an option that is gone. Every check
// throws before the caller writes anything. Per request, because it uses the database.
[RegisterPerRequest]
public class PollEdits(PollReader reader, PollRows rows, IAudienceRelay relay, IPollPublisher publisher)
{
    // True when a round is really open (not a draft) on that slide in the deck's live audience session.
    public async Task<bool> IsRoundOpenAsync(string deckId, Guid slideId)
        => await reader.LiveSessionAsync(deckId) is { } session
            && await rows.OpenRoundAsync(session) is { OpenedAt: not null } round
            && round.SlideId == slideId;

    // The code a refused edit starts its message with when a round is open on the slide: nothing can close a round whose slide
    // is gone or no longer a poll, because IPolls addresses a poll by its slide's number, so the caller closes the poll first.
    public const string POLL_OPEN = "poll_open";

    // Refuses, before the caller writes anything, when a round is really open on the slide (a draft or a closed round doesn't
    // block): what the slide is about to stop being, such as "deleted" or "a poll". Slides can't close the round itself,
    // because the lifecycle is IPolls' and runs in a transaction of its own.
    public async Task RequireNoOpenRoundAsync(string deckId, Slide slide, string change)
    {
        if (slide.Layout == PollRows.LAYOUT && await IsRoundOpenAsync(deckId, slide.Id))
        {
            throw new ConflictException($"{POLL_OPEN}: a round of this poll is open, so the slide can't be {change}; close the poll first (close_poll)");
        }
    }

    // Checks update_slide's new layout and slots against the slide as it is. Returns true when the slide is a poll before
    // and after, the deck is live, and the question or an option changed, so the caller announces it after its commit.
    public async Task<bool> CheckSlideEditAsync(string deckId, Slide slide, string newLayout, JsonElement newSlots)
    {
        var wasPoll = slide.Layout == PollRows.LAYOUT;
        var willBePoll = newLayout == PollRows.LAYOUT;
        if (!wasPoll && !willBePoll)
        {
            return false;
        }
        if (!willBePoll)
        {
            await RequireNoOpenRoundAsync(deckId, slide, "changed to another layout");
            return false;
        }
        var open = wasPoll && await IsRoundOpenAsync(deckId, slide.Id);

        var (question, options) = PollReader.SlotsOf(SlideJson.ForStorage(newSlots));
        var settings = await rows.SettingsAsync(slide.Id) ?? PollSettings.Default;
        if (PollSettingsRules.Check(settings, options.Count) is { } broken)
        {
            throw new BadRequestException(wasPoll
                ? $"slots.options: {broken}; change the poll's settings with update_poll first"
                : $"slots.options: this slide's saved poll settings don't fit its options ({broken}); give it options they fit");
        }
        if (!wasPoll)
        {
            return false;
        }

        var (oldQuestion, oldOptions) = PollReader.SlotsOf(slide.SlotsJson);
        if (open)
        {
            CheckOptionsKeepTheirPlaces(oldOptions, options);
        }
        var changed = question != oldQuestion || !options.SequenceEqual(oldOptions, StringComparer.Ordinal);
        return changed && await reader.LiveSessionAsync(deckId) != null;
    }

    // After the commit: the audience state, then the poll events, one after the other (V4).
    public async Task AnnounceAsync(string deckId, int slide)
    {
        await relay.RefreshAsync(deckId);
        await publisher.PublishAsync(deckId, slide);
    }

    // While a round is open the options are known by position, so what a position says can't move to another: the count can't
    // shrink, and no position may take the text another position had (trimmed, ignoring case). That catches a swap, a shift
    // (removing an option by moving the rest up) and a plain reorder, even when one more text is reworded at the same time;
    // rewording an option in place and appending still pass. Rewording two options into each other's places at once can't
    // be told from two rewords, and is allowed.
    private static void CheckOptionsKeepTheirPlaces(IReadOnlyList<string> was, IReadOnlyList<string> now)
    {
        if (now.Count < was.Count)
        {
            throw new BadRequestException($"slots.options: a round is open, so the options can't shrink from {was.Count} to {now.Count}; close the poll first (close_poll)");
        }
        for (var position = 0; position < was.Count; position++)
        {
            var text = Normal(now[position]);
            if (text == Normal(was[position]))
            {
                continue;
            }
            var taken = was.Select(Normal).ToList().IndexOf(text);
            if (taken >= 0)
            {
                throw new BadRequestException(
                    $"slots.options: a round is open, so option {position + 1} can't take option {taken + 1}'s text, which would move answers between options; reword options in place, or close the poll first (close_poll)");
            }
        }
    }

    private static string Normal(string text) => text.Trim().ToLowerInvariant();
}
