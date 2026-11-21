using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// SW-286: a join is answered from the state the relay last composed, so what changes outside the presenter's moves must
// still reach the guest who joins after it. Each test joins once (so a state is held), changes the deck's poll or Q&A through
// the real routes, then reads the join page again as a new guest would. The real poll, Q&A and relay services run; only the
// presenter's position is set in SettablePresentations.
[TestFixture]
[CancelAfter(60000)]
public class AudienceJoinCacheTests : QaTestFixture
{
    [Test]
    public async Task Join_WhenAPollClosesAfterAnEarlierJoin_ShouldShowTheClosedPollToTheNextGuest()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await MoveAsync(poll.DeckId, poll.Slide);
        var before = await JoinStateAsync(poll.Code);

        var closed = await Client.PostAsync($"/api/decks/{poll.DeckId}/polls/{poll.Slide}/close", null);
        var after = await JoinStateAsync(poll.Code);

        closed.IsSuccessStatusCode.ShouldBeTrue(await closed.Content.ReadAsStringAsync());
        before.GetProperty("activity").GetProperty("phase").GetString().ShouldBe("open");
        after.GetProperty("activity").GetProperty("phase").GetString().ShouldBe("closed");
        after.GetProperty("seq").GetInt64().ShouldBeGreaterThan(before.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task Join_WhenAQuestionIsAskedAfterAnEarlierJoin_ShouldCarryTheNewListRevisionToTheNextGuest()
    {
        var qa = await SeedQaAsync();
        var before = await JoinStateAsync(qa.Code);
        await using var asker = await GuestClient.ConnectAsync(Factory);
        await asker.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);

        var asked = await asker.AskAsync(qa.Code, new AskRequest("Does a join read the database?", null));
        var after = await JoinStateAsync(qa.Code);

        asked.IsSuccessStatusCode.ShouldBeTrue(await asked.Content.ReadAsStringAsync());
        after.GetProperty("room").GetProperty("listRevision").GetInt64()
            .ShouldBeGreaterThan(before.GetProperty("room").GetProperty("listRevision").GetInt64());
        after.GetProperty("seq").GetInt64().ShouldBeGreaterThan(before.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task Join_WhenTheDeckIsEditedAfterAnEarlierJoin_ShouldCarryTheNewRevisionAndSlideCountToTheNextGuest()
    {
        var qa = await SeedQaAsync();
        var before = await JoinStateAsync(qa.Code);

        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            await DeckSeed.AddQuoteAsync(agent, qa.DeckId, "A slide added while live");
        }
        var after = await JoinStateAsync(qa.Code);

        after.GetProperty("revision").GetInt64().ShouldBeGreaterThan(before.GetProperty("revision").GetInt64());
        after.GetProperty("current").GetProperty("count").GetInt32().ShouldBe(before.GetProperty("current").GetProperty("count").GetInt32() + 1);
    }

    [Test]
    public async Task Join_WhenAskedTwiceWithNothingChanged_ShouldKeepTheStateAndRaiseTheSeq()
    {
        var qa = await SeedQaAsync();

        var first = await JoinStateAsync(qa.Code);
        var second = await JoinStateAsync(qa.Code);

        second.GetProperty("seq").GetInt64().ShouldBeGreaterThan(first.GetProperty("seq").GetInt64());
        second.GetProperty("current").GetRawText().ShouldBe(first.GetProperty("current").GetRawText());
        second.GetProperty("room").GetRawText().ShouldBe(first.GetProperty("room").GetRawText());
    }

    // audienceState as the join page reads it.
    private async Task<JsonElement> JoinStateAsync(string code)
    {
        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/join/{code}");
        return body.GetProperty("state");
    }
}
