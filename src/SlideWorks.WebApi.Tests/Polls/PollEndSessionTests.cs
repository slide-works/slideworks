using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// Ending the live session with IAudienceSessions stood in by FakeAudienceSessions, whose session ids need not be GUIDs:
// IPolls.EndSessionAsync gets the session from LiveCommands.EndAsync, which holds the session row, so it never asks
// IAudienceSessions and never parses its id. The relay and the publisher are fakes, so nothing else reads the session.
[TestFixture]
public class PollEndSessionTests : PollTestFixture
{
    private FakeAudienceSessions Sessions => Factory.Services.GetRequiredService<FakeAudienceSessions>();

    private FakePollPublisher Publisher => Factory.Services.GetRequiredService<FakePollPublisher>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
        FakePollPublisher.AddTo(services);
        FakePollTallies.AddTo(services);
    }

    [Test]
    public async Task EndLive_WhenSessionsGiveANonGuidId_ShouldCloseTheRoundWithoutThrowing()
    {
        var poll = await SeedPollAsync();
        Sessions.Set(poll.Code, poll.DeckId, "live-session", AudiencePhase.Live);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest one", 0);

        var response = await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live");
        await using var scope = Factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PollRounds.AsNoTracking().SingleAsync(r => r.Id == round.Id);

        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        stored.ClosedAt.ShouldNotBeNull();
        stored.Final.ShouldNotBeNull();
        stored.Final.Answered.ShouldBe(1);
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide]);
    }
}
