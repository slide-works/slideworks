using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// GET and PUT /api/decks/{id}/sharing: the sharing dialog (src/contracts/screens/sharing.json). Level goes on the wire as
// "private", "link" or "named" (every enum is camelCase on the wire), and Link is the absolute share link from
// IPublicUrls. Viewers lists every named email; People adds each one's name, avatar and access (C4).
public sealed record SharingView(
    DeckSharing Level,
    IReadOnlyList<string> Viewers,
    string Link,
    SharingLastChange? LastChange,
    SharingOwnerView Owner,
    IReadOnlyList<SharingPersonView> People);
