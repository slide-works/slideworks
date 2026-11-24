using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The share log's own store test (C7): recorded and read through IShareRecipients from the fixture's services.
[TestFixture]
public class ShareRecipientsTests : ApiTestFixture
{
    [Test]
    public async Task DistinctSinceAsync_WhenSameEmailOnTwoDecks_ShouldReturnItOnceWithLatestTime()
    {
        var sharer = Guid.NewGuid();
        var start = Clock.GetUtcNow();
        await RecordAsync(sharer, "deckA", "Alex@Team.Example");
        Clock.Advance(TimeSpan.FromMinutes(10));
        await RecordAsync(sharer, "deckB", "alex@team.example");

        var recent = await DistinctSinceAsync(sharer, start);

        recent.ShouldBe([new RecentRecipient("alex@team.example", start.AddMinutes(10))]);
    }

    [Test]
    public async Task DistinctSinceAsync_WhenRecordedBeforeSince_ShouldLeaveItOut()
    {
        var sharer = Guid.NewGuid();
        await RecordAsync(sharer, "deckA", "old@team.example");
        Clock.Advance(TimeSpan.FromHours(2));
        var since = Clock.GetUtcNow();
        await RecordAsync(sharer, "deckA", "new@team.example");

        var recent = await DistinctSinceAsync(sharer, since);

        recent.Select(r => r.Email).ShouldBe(["new@team.example"]);
    }

    [Test]
    public async Task DistinctSinceAsync_WhenRecordedAtSince_ShouldKeepItOldestFirst()
    {
        var sharer = Guid.NewGuid();
        var since = Clock.GetUtcNow();
        await RecordAsync(sharer, "deckA", "first@team.example");
        Clock.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync(sharer, "deckA", "second@team.example");

        var recent = await DistinctSinceAsync(sharer, since);

        recent.Select(r => r.Email).ShouldBe(["first@team.example", "second@team.example"]);
    }

    [Test]
    public async Task DistinctSinceAsync_WhenOtherSharer_ShouldLeaveItOut()
    {
        var sharer = Guid.NewGuid();
        var since = Clock.GetUtcNow();
        await RecordAsync(sharer, "deckA", "mine@team.example");
        await RecordAsync(Guid.NewGuid(), "deckZ", "theirs@team.example");

        var recent = await DistinctSinceAsync(sharer, since);

        recent.Select(r => r.Email).ShouldBe(["mine@team.example"]);
    }

    [Test]
    public async Task RecordAsync_WhenNoEmails_ShouldWriteNothing()
    {
        var sharer = Guid.NewGuid();
        var since = Clock.GetUtcNow();

        await RecordAsync(sharer, "deckA");

        (await DistinctSinceAsync(sharer, since)).ShouldBeEmpty();
    }

    private async Task RecordAsync(Guid sharer, string deckId, params string[] emails)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IShareRecipients>().RecordAsync(sharer, deckId, emails);
    }

    private async Task<IReadOnlyList<RecentRecipient>> DistinctSinceAsync(Guid sharer, DateTimeOffset since)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IShareRecipients>().DistinctSinceAsync(sharer, since);
    }
}
