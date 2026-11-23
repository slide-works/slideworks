using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Accounts.Profile.Views;
using SlideWorks.WebApi.Features.OAuth.ConnectedAgents;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// The profile writes (C3): each delegates to its seam for the signed-in person, then answers with the refreshed profile.
// The seams own validation (name length, colour key, time zone id, photo type and size); until they are built they throw.
[RegisterPerRequest]
public class ProfileActions(
    ICurrentActor actor,
    IUserAccounts accounts,
    IProfileCommands commands,
    ITimeZones timeZones,
    IProfilePhotos photos,
    IConnectedAgents agents,
    ProfileViews views)
{
    private Guid Me => actor.PresenterId!.Value;

    public async Task<ProfileView> UpdateAsync(ProfileUpdate update)
    {
        await commands.SetNameAsync(Me, update.Name);
        await commands.SetColourAsync(Me, update.Colour);
        return await views.GetAsync();
    }

    // fromBrowser only fills an empty zone: for someone who already has one it changes nothing.
    public async Task<ProfileView> SetTimeZoneAsync(TimeZoneUpdate update)
    {
        var user = await accounts.FindAsync(Me) ?? throw new NotFoundException("The signed-in person no longer exists.");
        if (!update.FromBrowser || user.TimeZone == null)
        {
            await timeZones.SetAsync(Me, update.TimeZone);
        }
        return await views.GetAsync();
    }

    public async Task<ProfileView> UploadPhotoAsync(Stream body, string? contentType, CancellationToken cancellationToken)
    {
        await photos.UploadAsync(Me, body, contentType ?? "", cancellationToken);
        return await views.GetAsync();
    }

    public async Task<ProfileView> RemovePhotoAsync()
    {
        await photos.RemoveAsync(Me);
        return await views.GetAsync();
    }

    public async Task DisconnectAgentAsync(Guid familyId)
    {
        if (!await agents.DisconnectAsync(Me, familyId))
        {
            throw new NotFoundException("Agent not found");
        }
    }
}
