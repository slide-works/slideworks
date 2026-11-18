using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The pump in the real host, with no double on any seam (Q5): a guest's upvote goes through the real route, tally, pump,
// publisher and relay, and another guest's phone gets the questionTally. It fails when the pump is not registered, which no
// test with a fake publisher can show. The app is shared by the fixture, so the test seeds a deck of its own.
[TestFixture]
[CancelAfter(60000)]
public class QuestionPumpWiringTests : QaTestFixture
{
    private const string FIRST = "How do you split a big change?";

    private const string SECOND = "What is the smallest slice you have shipped?";

    [Test]
    public async Task Vote_WhenAGuestUpvotes_ShouldReachAnotherGuestAsAQuestionTally()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var voter = await GuestClient.ConnectAsync(Factory);
        await using var watcher = await GuestClient.ConnectAsync(Factory);
        await voter.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);
        await watcher.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);

        var response = await voter.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [2], 0));
        var frame = await HubReceiver.NextQaEventAsync(watcher.NextAudienceAsync);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        frame.GetProperty("type").GetString().ShouldBe("questionTally");
        frame.GetProperty("votes").EnumerateArray().Select(v => (v.GetProperty("number").GetInt32(), v.GetProperty("votes").GetInt32())).ShouldBe([(1, 0), (2, 1)]);
        frame.GetRawText().ShouldNotContain(voter.GuestId);
        frame.GetRawText().ShouldNotContain(watcher.GuestId);
    }
}
