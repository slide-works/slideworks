using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts;

// GET /api/me (S5) from the stored person, read by id: their access, whether they are an admin, how many requests wait
// for an admin (0 for anyone else) and whether the server has no admin yet.
[RegisterPerRequest]
public class MeViews(IUserAccounts accounts, ICurrentActor actor)
{
    public async Task<MeView> GetAsync()
    {
        var user = await accounts.FindAsync(actor.PresenterId!.Value)
            ?? throw new NotFoundException("The signed-in person no longer exists.");
        return MeView.From(
            user,
            access: user.Access == UserAccess.Pending ? "pending" : "approved",
            isAdmin: user.IsAdmin,
            pendingRequests: user.IsAdmin ? await accounts.PendingCountAsync() : 0,
            adminMissing: !await accounts.AnyAdminAsync());
    }
}
