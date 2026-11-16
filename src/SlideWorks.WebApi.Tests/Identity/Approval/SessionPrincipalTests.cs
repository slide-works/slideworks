using System.Security.Claims;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// SessionPrincipal.For (S2) builds every person's principal from the stored user. It is a pure function of the user, so
// it is checked directly.
[TestFixture]
public class SessionPrincipalTests
{
    [TestCase(UserAccess.Pending, false)]
    [TestCase(UserAccess.Approved, false)]
    [TestCase(UserAccess.Approved, true)]
    public void For_ShouldCarryAccessAndAdmin(UserAccess access, bool isAdmin)
    {
        var user = NewUser(access, isAdmin);

        var principal = SessionPrincipal.For(user, "Cookies");

        principal.Identity!.AuthenticationType.ShouldBe("Cookies");
        principal.Identity.IsAuthenticated.ShouldBeTrue();
        principal.FindFirstValue(SlideWorksClaims.PRESENTER_ID).ShouldBe(user.Id.ToString());
        principal.FindFirstValue(SlideWorksClaims.EMAIL).ShouldBe(user.Email);
        principal.FindFirstValue(SlideWorksClaims.NAME).ShouldBe(user.Name);
        principal.FindFirstValue(SlideWorksClaims.ACTOR_KIND).ShouldBe("Person");
        principal.FindFirstValue(SlideWorksClaims.ACCESS).ShouldBe(access.ToString());
        principal.FindAll(SlideWorksClaims.IS_ADMIN).Select(c => c.Value).ShouldBe(isAdmin ? ["true"] : []);
    }

    [Test]
    public void For_WhenRejected_ShouldThrow()
    {
        var user = NewUser(UserAccess.Rejected, isAdmin: false);

        Should.Throw<InvalidOperationException>(() => SessionPrincipal.For(user, "Cookies"));
    }

    private static User NewUser(UserAccess access, bool isAdmin) => new()
    {
        Id = Guid.NewGuid(),
        Email = "lee@team.example",
        Name = "Lee Park",
        GoogleSubject = "google-sub-lee",
        Access = access,
        IsAdmin = isAdmin,
    };
}
