using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// TODO: validate and save the name and colour on the user, with the name-set-by-person flag.
[RegisterPerRequest]
public sealed class ProfileCommands : IProfileCommands
{
    public Task SetNameAsync(Guid userId, string name) => throw new NotImplementedException("Profile editing is not built yet.");

    public Task SetColourAsync(Guid userId, string? colour) => throw new NotImplementedException("Profile editing is not built yet.");
}
