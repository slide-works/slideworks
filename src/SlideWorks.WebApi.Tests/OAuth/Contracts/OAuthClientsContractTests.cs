using SlideWorks.WebApi.Features.OAuth.Stores;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 IOAuthClients: dynamic registration, then lookup by client_id.
[TestFixture]
public class OAuthClientsContractTests : OAuthContractFixture
{
    [Test]
    public async Task Register_ShouldBeFoundByItsClientId()
    {
        var registered = await UseAsync<IOAuthClients, OAuthClient>(c => c.RegisterAsync(new ClientRegistration("Claude", [REDIRECT, "http://localhost:8080/cb"])));

        var found = await UseAsync<IOAuthClients, OAuthClient?>(c => c.FindAsync(registered.ClientId));

        found.ShouldNotBeNull();
        found.ClientId.ShouldBe(registered.ClientId);
        found.ClientName.ShouldBe("Claude");
        found.RedirectUris.ShouldBe([REDIRECT, "http://localhost:8080/cb"]);
        found.CreatedAt.ShouldBe(Clock.GetUtcNow(), TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task Register_WhenTwice_ShouldGiveDifferentUnguessableIds()
    {
        var first = await RegisterClientAsync();
        var second = await RegisterClientAsync();

        second.ClientId.ShouldNotBe(first.ClientId);
        first.ClientId.Length.ShouldBeGreaterThanOrEqualTo(22);
    }

    [Test]
    public async Task Find_WhenUnknown_ShouldReturnNull()
    {
        (await UseAsync<IOAuthClients, OAuthClient?>(c => c.FindAsync("not-a-client"))).ShouldBeNull();
    }
}
