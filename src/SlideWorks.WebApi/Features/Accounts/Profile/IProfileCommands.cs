namespace SlideWorks.WebApi.Features.Accounts.Profile;

public interface IProfileCommands
{
    // An explicit JSON null for either is refused like an empty name or unknown colour. Validates the name (trimmed, 1 to 60 characters) and the colour (a palette key) first, then saves both in one write
    // and marks the name as chosen by the person, so a later Google sign-in keeps it. A refusal changes nothing.
    Task SaveAsync(Guid userId, string? name, string? colour);
}
