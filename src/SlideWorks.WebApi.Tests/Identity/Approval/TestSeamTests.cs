using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// The test people (S7), signed in through TestAuthScheme as every API test is: Sam, Alex and Mira approved, Ada the
// admin through SLIDEWORKS_ADMIN_EMAIL, NewPending() people left waiting, and a rejected person signed out.
[TestFixture]
public class TestSeamTests : ApiTestFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => ActorProbeController.AddTo;

    [Test]
    public async Task NamedPeople_ShouldBeApproved()
    {
        foreach (var person in new[] { TestUser.Sam, TestUser.Alex, TestUser.Mira })
        {
            var actor = await ProbeAsync(person);

            actor.IsSignedIn.ShouldBeTrue();
            actor.Email.ShouldBe(person.Email);
            actor.Access.ShouldBe(nameof(UserAccess.Approved));
            actor.IsAdmin.ShouldBeFalse();
        }
    }

    [Test]
    public async Task Ada_ShouldBeAdmin()
    {
        var actor = await ProbeAsync(TestUser.Ada);

        actor.Access.ShouldBe(nameof(UserAccess.Approved));
        actor.IsAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task NewPending_ShouldStayPending()
    {
        var person = TestUser.NewPending();

        await ProbeAsync(person);
        var again = await ProbeAsync(person);

        person.Email.ShouldEndWith("@" + TestUser.PENDING_DOMAIN);
        again.IsSignedIn.ShouldBeTrue();
        again.Email.ShouldBe(person.Email);
        again.Access.ShouldBe(nameof(UserAccess.Pending));
        again.IsAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task Rejected_ShouldBeSignedOut()
    {
        var person = TestUser.NewPending();
        await ProbeAsync(person);
        using (var scope = Factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<IUserDirectory>().FindByEmailAsync(person.Email);
            (await scope.ServiceProvider.GetRequiredService<IUserAccounts>().RejectAsync(user!.Id, by: null)).ShouldBe(AccountChange.Changed);
        }

        var actor = await ProbeAsync(person);
        using var client = ClientFor(person);
        var me = await client.GetAsync("/api/me");

        actor.IsSignedIn.ShouldBeFalse();
        actor.Access.ShouldBeNull();
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<ActorDto> ProbeAsync(TestUser person)
    {
        using var client = ClientFor(person);
        return (await client.GetFromJsonAsync<ActorDto>(ActorProbeController.PATH))!;
    }
}
