using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts;

// GET /api/me (S5) from the stored person, read by id: their access, whether they are an admin, how many requests wait
// for an admin (0 for anyone else) and whether the server has no admin yet.
[RegisterPerRequest]
public class MeViews(IUserAccounts accounts, ICurrentActor actor, IAvatars avatars)
{
    public Task<MeView> GetAsync() => ForAsync(actor.PresenterId!.Value);

    // The same view for a person named by id, for a caller who isn't signed in as them yet (a phone's device-session
    // exchange, K4).
    public async Task<MeView> ForAsync(Guid userId)
    {
        var user = await accounts.FindAsync(userId)
            ?? throw new NotFoundException("The signed-in person no longer exists.");
        return MeView.From(
            user,
            avatars.For(user),
            access: user.Access == UserAccess.Pending ? "pending" : "approved",
            isAdmin: user.IsAdmin,
            pendingRequests: user.IsAdmin ? await accounts.PendingCountAsync() : 0,
            adminMissing: !await accounts.AnyAdminAsync());
    }
}
