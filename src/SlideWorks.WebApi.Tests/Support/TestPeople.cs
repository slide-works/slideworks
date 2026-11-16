using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// How every test seam creates a person (S7): through the real IUserDirectory, as a sign-in would, so a new person is
// Pending unless the SLIDEWORKS_ADMIN_EMAIL bootstrap makes them the admin. Sam, Alex and Mira are then approved through
// IUserAccounts (by the system), so tests written before approval existed still act as approved people. Ada is approved
// only as the bootstrap admin, and NewPending() people never are.
public static class TestPeople
{
    private static readonly TestUser[] APPROVED_ON_SIGN_IN = [TestUser.Sam, TestUser.Alex, TestUser.Mira];

    // services is a request's or a scope's provider. Returns the stored user as it is after sign-in.
    public static async Task<User> SignInAsync(IServiceProvider services, TestUser person)
    {
        if (person.IsAnonymous)
        {
            throw new ArgumentException("Anonymous never signs in.", nameof(person));
        }
        var user = await services.GetRequiredService<IUserDirectory>().UpsertAsync(person.GoogleSubject, person.Email, person.Name, pictureUrl: null);
        if (user.Access != UserAccess.Pending || !APPROVED_ON_SIGN_IN.Contains(person))
        {
            return user;
        }
        var accounts = services.GetRequiredService<IUserAccounts>();
        // NotAllowed here means a parallel first sign-in approved them first; the read below shows it.
        if (await accounts.ApproveAsync(user.Id, by: null) == AccountChange.NotFound)
        {
            throw new InvalidOperationException($"{person} vanished during sign-in.");
        }
        return await accounts.FindAsync(user.Id) ?? throw new InvalidOperationException($"{person} vanished during sign-in.");
    }
}
