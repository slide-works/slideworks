namespace SlideWorks.Core.Rendering;

// Short-lived tokens that let a headless browser read one deck for one purpose (L9). A token holds no server
// state, so it can be reused until it expires.
public interface IRenderTokens
{
    string Issue(string deckId, RenderPurpose purpose);

    // True only for a token issued for this deck and this purpose that has not expired. A null, empty or
    // malformed token is false, never an exception.
    bool Validate(string? token, string deckId, RenderPurpose purpose);
}
