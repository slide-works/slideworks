using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 IOAuthRequests: a pending authorize request is bound to its presenter, lives 10 minutes and is consumed once.
// FindAsync reads it for the consent screen under the same rules, without consuming it.
[TestFixture]
public class OAuthRequestsContractTests : OAuthContractFixture
{
    [Test]
    public async Task Consume_WhenPresenterMatches_ShouldReturnTheRequestAsCreated()
    {
        var (pending, requestId) = await CreateAsync();

        var consumed = await ConsumeAsync(requestId, pending.PresenterId);

        consumed.ShouldBe(pending);
    }

    [Test]
    public async Task Create_WhenTwice_ShouldGiveDifferentIds()
    {
        var (_, first) = await CreateAsync();
        var (_, second) = await CreateAsync();

        second.ShouldNotBe(first);
    }

    [Test]
    public async Task Consume_WhenAlreadyConsumed_ShouldReturnNull()
    {
        var (pending, requestId) = await CreateAsync();
        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldNotBeNull();

        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldBeNull();
    }

    [Test]
    public async Task Consume_WhenAnotherPresenter_ShouldReturnNullAndLeaveItForTheOwner()
    {
        var (pending, requestId) = await CreateAsync();
        var alex = await PresenterAsync(TestUser.Alex);

        (await ConsumeAsync(requestId, alex)).ShouldBeNull();

        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldBe(pending);
    }

    [Test]
    public async Task Consume_WhenUnknown_ShouldReturnNull()
    {
        var sam = await PresenterAsync(TestUser.Sam);

        (await ConsumeAsync("not-a-request", sam)).ShouldBeNull();
    }

    [Test]
    public async Task Consume_WhenJustBeforeTenMinutes_ShouldReturnTheRequest()
    {
        var (pending, requestId) = await CreateAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldBe(pending);
    }

    [Test]
    public async Task Consume_WhenTenMinutesHavePassed_ShouldReturnNull()
    {
        var (pending, requestId) = await CreateAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldBeNull();
    }

    [Test]
    public async Task Consume_WhenTwentyRace_ShouldSucceedExactlyOnce()
    {
        var (pending, requestId) = await CreateAsync();

        var results = await RaceAsync<IOAuthRequests, PendingAuthorization?>(r => r.ConsumeAsync(requestId, pending.PresenterId));

        results.Count(r => r != null).ShouldBe(1);
    }

    [Test]
    public async Task Find_WhenPresenterMatches_ShouldReturnTheRequestAndLeaveItToConsume()
    {
        var (pending, requestId) = await CreateAsync();

        (await FindAsync(requestId, pending.PresenterId)).ShouldBe(pending);
        (await FindAsync(requestId, pending.PresenterId)).ShouldBe(pending);

        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldBe(pending);
    }

    [Test]
    public async Task Find_WhenAnotherPresenter_ShouldReturnNull()
    {
        var (pending, requestId) = await CreateAsync();
        var alex = await PresenterAsync(TestUser.Alex);

        (await FindAsync(requestId, alex)).ShouldBeNull();

        (await FindAsync(requestId, pending.PresenterId)).ShouldBe(pending);
    }

    [Test]
    public async Task Find_WhenConsumed_ShouldReturnNull()
    {
        var (pending, requestId) = await CreateAsync();
        (await ConsumeAsync(requestId, pending.PresenterId)).ShouldNotBeNull();

        (await FindAsync(requestId, pending.PresenterId)).ShouldBeNull();
    }

    [Test]
    public async Task Find_WhenUnknown_ShouldReturnNull()
    {
        var sam = await PresenterAsync(TestUser.Sam);

        (await FindAsync("not-a-request", sam)).ShouldBeNull();
    }

    [Test]
    public async Task Find_WhenJustBeforeTenMinutes_ShouldReturnTheRequest()
    {
        var (pending, requestId) = await CreateAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        (await FindAsync(requestId, pending.PresenterId)).ShouldBe(pending);
    }

    [Test]
    public async Task Find_WhenTenMinutesHavePassed_ShouldReturnNull()
    {
        var (pending, requestId) = await CreateAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        (await FindAsync(requestId, pending.PresenterId)).ShouldBeNull();
    }

    private async Task<(PendingAuthorization Pending, string RequestId)> CreateAsync()
    {
        var client = await RegisterClientAsync();
        var sam = await PresenterAsync(TestUser.Sam);
        var pending = new PendingAuthorization(client.ClientId, sam, REDIRECT, "client-state-123", CHALLENGE, SCOPE, Resource);
        return (pending, await UseAsync<IOAuthRequests, string>(r => r.CreateAsync(pending)));
    }

    private Task<PendingAuthorization?> FindAsync(string requestId, Guid presenterId)
        => UseAsync<IOAuthRequests, PendingAuthorization?>(r => r.FindAsync(requestId, presenterId));

    private Task<PendingAuthorization?> ConsumeAsync(string requestId, Guid presenterId)
        => UseAsync<IOAuthRequests, PendingAuthorization?>(r => r.ConsumeAsync(requestId, presenterId));
}
