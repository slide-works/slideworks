using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// The word-cloud publisher over rows seeded through the DbContext in a live session: one presenter event with every word
// and one audience event with the hidden words left out, in that order, on the audience bus.
[TestFixture]
[CancelAfter(60000)]
public class WordCloudTests : ApiTestFixture
{
    [Test]
    public async Task Cloud_WhenTwoGuestsSendTheSameWord_ShouldCountTwo(CancellationToken cancellationToken)
    {
        var (deck, slide, round) = await OpenRoundAsync(2, false);
        await SeedAsync(round, ("guest-a", 0, "Calm", false), ("guest-a", 1, "Busy", false), ("guest-b", 0, "calm", false));
        var received = Listen(deck, out var subscription);
        using var _ = subscription;

        await Factory.Services.GetRequiredService<IWordCloudPublisher>().PublishAsync(deck, slide, cancellationToken);
        var presenter = (WordCloudTally)await received.ReadAsync(cancellationToken);
        var audience = (WordCloudTally)await received.ReadAsync(cancellationToken);

        presenter.Target.ShouldBe(OpenResponseTarget.Presenter);
        audience.Target.ShouldBe(OpenResponseTarget.Audience);
        audience.Slide.ShouldBe(slide);
        audience.Phase.ShouldBe(OpenResponsePhase.Open);
        audience.Round.ShouldBe(round.ToString());
        audience.Number.ShouldBe(1);
        audience.Revision.ShouldBe(2);
        audience.Revealed.ShouldBeTrue();
        audience.Guests.ShouldBe(2);
        audience.Words.ShouldBe([new CloudWord("Calm", 2, false), new CloudWord("Busy", 1, false)]);
        presenter.Words.ShouldBe([new CloudWord("Calm", 2, false), new CloudWord("Busy", 1, false)]);
    }

    [Test]
    public async Task Cloud_WhenAWordIsHidden_ShouldOmitItFromTheAudience(CancellationToken cancellationToken)
    {
        var (deck, slide, round) = await OpenRoundAsync(4, false);
        await SeedAsync(round, ("guest-a", 0, "Calm", false), ("guest-b", 0, "Rubbish", true), ("guest-c", 0, "Rubbish", true));
        var received = Listen(deck, out var subscription);
        using var _ = subscription;

        await Factory.Services.GetRequiredService<IWordCloudPublisher>().PublishAsync(deck, slide, cancellationToken);
        var presenter = (WordCloudTally)await received.ReadAsync(cancellationToken);
        var audience = (WordCloudTally)await received.ReadAsync(cancellationToken);

        presenter.Target.ShouldBe(OpenResponseTarget.Presenter);
        presenter.Words.ShouldBe([new CloudWord("Rubbish", 2, true), new CloudWord("Calm", 1, false)]);
        presenter.Guests.ShouldBe(1);
        audience.Target.ShouldBe(OpenResponseTarget.Audience);
        audience.Words.ShouldBe([new CloudWord("Calm", 1, false)]);
        audience.Guests.ShouldBe(1);
    }

    [Test]
    public async Task Cloud_WhenTheReadThrows_ShouldNotPublishAnEmptyCloud(CancellationToken cancellationToken)
    {
        var (deck, slide, round) = await OpenRoundAsync(1, true);
        await SeedAsync(round, ("guest-a", 0, "Calm", false));
        var received = Listen(deck, out var subscription);
        using var _ = subscription;
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        var sentinel = new JoinedCount(deck, 1);

        await Should.ThrowAsync<InvalidOperationException>(
            () => Factory.Services.GetRequiredService<IWordCloudPublisher>().PublishAsync(deck, slide, cancellationToken));
        await bus.PublishAsync(sentinel);
        var first = await received.ReadAsync(cancellationToken);

        first.ShouldBeSameAs(sentinel);
    }

    // Each event the bus carries for the deck, in the order it was published.
    private ChannelReader<AudienceEvent> Listen(string deck, out IDisposable subscription)
    {
        var channel = Channel.CreateUnbounded<AudienceEvent>();
        subscription = Factory.Services.GetRequiredService<IAudienceEvents>().Subscribe(deck, e => channel.Writer.WriteAsync(e).AsTask());
        return channel.Reader;
    }

    // A live deck whose slide is a word cloud with round 1 open at the revision, and with a second open round on the slide
    // too when broken, which no reader can pick between.
    private async Task<(string Deck, int Slide, Guid Round)> OpenRoundAsync(int revision, bool broken)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "word-cloud", """{"question":"One word for how this sprint felt?"}""");
        using var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.IsSuccessStatusCode.ShouldBeTrue(await live.Content.ReadAsStringAsync());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Slides.SingleAsync(s => s.DeckId == deck && s.Layout == "word-cloud");
        var session = await db.AudienceSessions.SingleAsync(s => s.DeckId == deck && s.EndedAt == null);
        var response = new OpenResponse { Id = Guid.NewGuid(), DeckId = deck, SlideId = row.Id, Kind = OpenResponseKind.WordCloud };
        var round = new OpenRound { Id = Guid.NewGuid(), OpenResponseId = response.Id, SessionId = session.Id, Number = 1, Revision = revision, OpenedAt = Clock.GetUtcNow() };
        db.OpenResponses.Add(response);
        db.OpenRounds.Add(round);
        if (broken)
        {
            db.OpenRounds.Add(new OpenRound { Id = Guid.NewGuid(), OpenResponseId = response.Id, SessionId = session.Id, Number = 2, Revision = 0, OpenedAt = Clock.GetUtcNow() });
        }
        await db.SaveChangesAsync();
        return (deck, row.Position + 1, round.Id);
    }

    private async Task SeedAsync(Guid round, params (string Guest, int Position, string Text, bool Hidden)[] rows)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OpenEntries.AddRange(rows.Select(r => new OpenEntry
        {
            Id = Guid.NewGuid(),
            RoundId = round,
            GuestId = r.Guest,
            Position = r.Position,
            Text = r.Text,
            TextKey = OpenTextRules.KeyOf(r.Text),
            HiddenAt = r.Hidden ? Clock.GetUtcNow() : null,
        }));
        await db.SaveChangesAsync();
    }
}
