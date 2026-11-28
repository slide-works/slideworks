using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.Translations;

namespace SlideWorks.WebApi.Realtime;

// The per-language slide-state groups (SW-883 D11). Every audience connection stays in audience:{deckId}, which carries
// everything but audienceState, and is also in exactly one audience-slide:{deckId}:{language} group, the one for its
// effective language: the language it picked when translations are on and the deck offers it, else the primary.
public static class AudienceSlideGroups
{
    public static string Name(string deckId, string language) => $"audience-slide:{deckId}:{language}";

    public static string Effective(TranslationSettings settings, string? chosen)
        => settings.Enabled && chosen is not null && settings.Languages.Contains(chosen, StringComparer.Ordinal) ? chosen : settings.Primary;

    // Puts the connection in the language's group and takes it out of the one it was in. It is added first, so no state is
    // missed in between; a move's state may then come twice with one seq, which a guest ignores as not newer.
    public static async Task PlaceAsync(IAudienceRoster roster, IGroupManager groups, string deckId, string connectionId, string language)
    {
        var before = await roster.PlaceAsync(connectionId, language);
        if (before is null || before.Placed == language)
        {
            return;
        }
        await groups.AddToGroupAsync(connectionId, Name(deckId, language));
        if (before.Placed is { } placed)
        {
            await groups.RemoveFromGroupAsync(connectionId, Name(deckId, placed));
        }
    }
}
