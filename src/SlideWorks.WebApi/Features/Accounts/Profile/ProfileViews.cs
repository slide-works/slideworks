using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Features.Accounts.Profile.Views;
using SlideWorks.WebApi.Features.OAuth.ConnectedAgents;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// Builds the profile screen (C3) for the signed-in person: identity and account from the stored user, agents from IConnectedAgents.
[RegisterPerRequest]
public class ProfileViews(IUserAccounts accounts, ICurrentActor actor, IAvatars avatars, ITimeZones timeZones, IConnectedAgents agents)
{
    private const string ADMIN_URL = "/admin/users";

    public async Task<ProfileView> GetAsync()
    {
        var userId = actor.PresenterId!.Value;
        var user = await accounts.FindAsync(userId)
            ?? throw new NotFoundException("The signed-in person no longer exists.");
        var connected = await agents.ListAsync(userId);
        return new ProfileView(
            new IdentityPanel(user.Name, user.Email, avatars.For(user), user.AvatarColour, AvatarColours.Keys, user.PhotoId != null),
            new AccountPanel(user.TimeZone, timeZones.List(), user.IsAdmin, ADMIN_URL),
            [.. connected.Select(a => new AgentRow(a.FamilyId, a.ClientName, a.Connected, a.LastUsed))]);
    }
}
