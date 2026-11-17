using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// IDeckAccess.CanPresentAsync (D3, P2): true only for the deck's owner signed in as a person. A viewer, a link viewer,
// an anonymous caller and the owner's own agent are false. Asked directly through the app's IDeckAccess, because an
// agent's bearer never reaches /api or the hub, so only this seam can tell an agent from an anonymous caller.
[TestFixture]
public class CanPresentTests : ApiTestFixture
{
    [Test]
    public async Task CanPresent_WhenOwnerPerson_ShouldBeTrue()
    {
        var (deck, ownerId) = await OwnDeckAsync();

        (await CanPresentAsync(deck, new Actor(ownerId, TestUser.Sam.Email, ActorKind.Person))).ShouldBeTrue();
    }

    [Test]
    public async Task CanPresent_WhenNamedViewer_ShouldBeFalse()
    {
        var (deck, _) = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);
        var alex = new Actor(Guid.NewGuid(), TestUser.Alex.Email, ActorKind.Person);

        (await AccessAsync(access => access.CanViewAsync(deck, alex))).ShouldBeTrue();
        (await CanPresentAsync(deck, alex)).ShouldBeFalse();
    }

    [Test]
    public async Task CanPresent_WhenLinkViewer_ShouldBeFalse()
    {
        var (deck, _) = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "link");
        var viewer = new Actor(Guid.NewGuid(), TestUser.Mira.Email, ActorKind.Person);

        (await AccessAsync(access => access.CanViewAsync(deck, viewer))).ShouldBeTrue();
        (await CanPresentAsync(deck, viewer)).ShouldBeFalse();
    }

    [Test]
    public async Task CanPresent_WhenAnonymous_ShouldBeFalse()
    {
        var (deck, _) = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "link");
        var anonymous = new Actor(null, null, ActorKind.Person);

        (await AccessAsync(access => access.CanViewAsync(deck, anonymous))).ShouldBeTrue();
        (await CanPresentAsync(deck, anonymous)).ShouldBeFalse();
    }

    [Test]
    public async Task CanPresent_WhenOwnersAgent_ShouldBeFalse()
    {
        var (deck, ownerId) = await OwnDeckAsync();
        var agent = new Actor(ownerId, TestUser.Sam.Email, ActorKind.Agent);

        (await AccessAsync(access => access.CanEditAsync(deck, agent))).ShouldBeTrue();
        (await CanPresentAsync(deck, agent)).ShouldBeFalse();
    }

    [Test]
    public async Task CanPresent_WhenUnknownDeck_ShouldBeFalse()
    {
        var (_, ownerId) = await OwnDeckAsync();

        (await CanPresentAsync("nOsUcHdEcK00", new Actor(ownerId, TestUser.Sam.Email, ActorKind.Person))).ShouldBeFalse();
    }

    private async Task<(string Deck, Guid OwnerId)> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return (await PresentingSeed.CreateDeckAsync(agent), agent.PresenterId);
    }

    private Task<bool> CanPresentAsync(string deck, ICurrentActor actor) => AccessAsync(access => access.CanPresentAsync(deck, actor));

    private async Task<bool> AccessAsync(Func<IDeckAccess, Task<bool>> ask)
    {
        using var scope = Factory.Services.CreateScope();
        return await ask(scope.ServiceProvider.GetRequiredService<IDeckAccess>());
    }

    // The caller as ICurrentActor describes it: a person or an agent acting for PresenterId, or anonymous.
    private sealed record Actor(Guid? PresenterId, string? Email, ActorKind Kind) : ICurrentActor
    {
        public bool IsSignedIn => PresenterId != null;

        public string? Name => null;

        public ActorVia Via => Kind == ActorKind.Agent ? ActorVia.Mcp : ActorVia.Web;

        public UserAccess? Access => IsSignedIn ? UserAccess.Approved : null;

        public bool IsAdmin => false;
    }
}
