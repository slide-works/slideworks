namespace SlideWorks.WebApi.Features.Accounts.Profile;

public interface IProfileCommands
{
    // Sets the person's display name and marks it as chosen by them, so a later Google sign-in keeps it.
    Task SetNameAsync(Guid userId, string name);

    // Sets the avatar colour (a palette key), or clears it with null.
    Task SetColourAsync(Guid userId, string? colour);
}
