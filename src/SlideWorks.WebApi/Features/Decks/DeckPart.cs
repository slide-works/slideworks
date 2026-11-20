namespace SlideWorks.WebApi.Features.Decks;

// One part of a deck (K19). Caption and Mark are null when unset; a part with a mark is an unnumbered section. Short
// falls back to the name, so a view always carries a short name; a stored short equal to the name is not stored.
public sealed record DeckPart(string Name, string? Short = null, string? Caption = null, string? Mark = null)
{
    public string? Short { get; init; } = Short ?? Name;
}
