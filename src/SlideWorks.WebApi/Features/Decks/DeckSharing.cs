namespace SlideWorks.WebApi.Features.Decks;

// Who can open a deck: only its owner, anyone with the link, or the named viewers.
public enum DeckSharing
{
    Private,
    Link,
    Named,
}
