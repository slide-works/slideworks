using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// How the real reactions store their counts (Q7, D19): totals per session, slide and emoji, written 5 seconds after the
// first unwritten tap and when the session ends, and one ReactionGuest row per guest who tapped, never a row per tap. Taps
// go through POST /api/join/{code}/reactions; the session's totals are read back through GET /api/decks/{id}/qa and the
// stored rows straight from the database. Each test seeds a live deck of its own (QaSeed, the presenter on the quote slide).
// The fixture's fake clock drives the 5 seconds, so nothing sleeps: the clock starts a write on its timer as it passes the
// 5 seconds, and a read of the totals waits for a write in progress (Q7: stored plus in memory, never counted twice), so a
// test reads the totals before it reads the rows.
[TestFixture]
[CancelAfter(60000)]
public class ReactionFlushTests : QaTestFixture
{
    [Test]
    public async Task TapAsync_WhenFirstOfTheSession_ShouldRecordTheGuestOnce()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa.Code);
        await using var other = await JoinAsync(qa.Code);

        await TapAsync(guest, qa.Code, "clap", "heart");
        Clock.Advance(ReactionFlusher.Interval);
        await TotalsAsync(qa.DeckId);
        var firstWrite = await StoredAsync(qa.SessionId);
        await TapAsync(guest, qa.Code, "clap");
        await TapAsync(other, qa.Code, "wow");
        Clock.Advance(ReactionFlusher.Interval);
        await TotalsAsync(qa.DeckId);
        var secondWrite = await StoredAsync(qa.SessionId);

        var guests = await StoredGuestsAsync(qa.SessionId);
        firstWrite.Sum(t => t.Count).ShouldBe(2);
        secondWrite.Sum(t => t.Count).ShouldBe(4);
        guests.Order(StringComparer.Ordinal).ShouldBe(new[] { guest.GuestId, other.GuestId }.Order(StringComparer.Ordinal));
    }

    [Test]
    public async Task EndSessionAsync_WhenTapsAreUnwritten_ShouldStoreTheTotalsPerSlide()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa.Code);
        await TapAsync(guest, qa.Code, "clap", "clap", "heart");
        Presentations.Set(SettablePresentations.At(qa.DeckId, qa.QaSlide));
        await TapAsync(guest, qa.Code, "wow");

        var stored = await StoredAsync(qa.SessionId);
        var end = await Client.DeleteAsync($"/api/decks/{qa.DeckId}/live");
        var afterEnd = await StoredAsync(qa.SessionId);

        stored.ShouldBeEmpty();
        end.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        afterEnd.ShouldBe(
            [(qa.QuoteSlideId, ReactionEmoji.Heart, 1), (qa.QuoteSlideId, ReactionEmoji.Clap, 2), (qa.QaSlideId, ReactionEmoji.Wow, 1)],
            ignoreOrder: true);
        (await StoredGuestsAsync(qa.SessionId)).ShouldBe([guest.GuestId]);
    }

    // The session's end writes what is in memory once: a second call, and the timer the taps started, add nothing.
    [Test]
    public async Task EndSessionAsync_WhenCalledAgain_ShouldWriteNothingMore()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa.Code);
        await TapAsync(guest, qa.Code, "laugh", "laugh");
        var reactions = Factory.Services.GetRequiredService<IReactions>();

        await reactions.EndSessionAsync(qa.SessionId);
        await reactions.EndSessionAsync(qa.SessionId);
        Clock.Advance(ReactionFlusher.Interval);
        var totals = await reactions.TotalsAsync(qa.SessionId);

        totals.ShouldBe(ReactionCounts.Zero with { Laugh = 2 });
        (await StoredAsync(qa.SessionId)).ShouldBe([(qa.QuoteSlideId, ReactionEmoji.Laugh, 2)]);
    }

    // Stored totals plus the taps in memory, and the same once those taps are written: nothing is missed or counted twice.
    [Test]
    public async Task TotalsAsync_WhenUnflushedTapsExist_ShouldIncludeThem()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.ReactionAsync(qa.SessionId, qa.QuoteSlideId, ReactionEmoji.Clap, 5);
        await using var guest = await JoinAsync(qa.Code);
        await TapAsync(guest, qa.Code, "clap", "clap", "thumbsUp");

        var beforeTheWrite = await TotalsAsync(qa.DeckId);
        Clock.Advance(ReactionFlusher.Interval);
        var afterTheWrite = await TotalsAsync(qa.DeckId);
        var stored = await StoredAsync(qa.SessionId);

        beforeTheWrite.ShouldBe(ReactionCounts.Zero with { Clap = 7, ThumbsUp = 1 });
        afterTheWrite.ShouldBe(beforeTheWrite);
        stored.ShouldBe([(qa.QuoteSlideId, ReactionEmoji.Clap, 7), (qa.QuoteSlideId, ReactionEmoji.ThumbsUp, 1)], ignoreOrder: true);
    }

    private async Task<GuestClient> JoinAsync(string code)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    private static async Task TapAsync(GuestClient guest, string code, params string[] emoji)
    {
        foreach (var e in emoji)
        {
            var response = await guest.ReactAsync(code, new ReactionRequest(e));
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, e);
        }
    }

    private async Task<ReactionCounts> TotalsAsync(string deckId)
    {
        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/qa");
        return body.GetProperty("reactions").Deserialize<ReactionCounts>(JsonSerializerOptions.Web)!;
    }

    private async Task<IReadOnlyList<(Guid SlideId, ReactionEmoji Emoji, int Count)>> StoredAsync(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.ReactionTotals.AsNoTracking().Where(t => t.SessionId == sessionId).ToListAsync();
        return [.. rows.Select(t => (t.SlideId, t.Emoji, t.Count))];
    }

    private async Task<IReadOnlyList<string>> StoredGuestsAsync(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ReactionGuests.AsNoTracking().Where(g => g.SessionId == sessionId).Select(g => g.GuestId).ToListAsync();
    }
}
