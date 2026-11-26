using Agoda.IoC.Core;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// Render tokens on a request (L9). A headless page sends its token in the X-Render-Token header. An <img> can't send a
// header, so the page first trades the header for the sw_render cookie (Start), which only /api/images and /api/templates ever receive.
// Either way the token opens only the one deck it was issued for, whatever its purpose: a token for another deck, an
// expired one or a forged one opens nothing, and the caller sees the same 404 as for an unknown deck or image.
[RegisterPerRequest]
public class RenderSessions
{
    public const string HEADER = "X-Render-Token";
    public const string COOKIE = "sw_render";
    // Images, and the files of a template (fonts, logos, art). Two narrow paths rather than /api, so the token reaches
    // no other route.
    public static readonly string[] COOKIE_PATHS = ["/api/images", "/api/templates"];

    private static readonly RenderPurpose[] PURPOSES = [RenderPurpose.Preview, RenderPurpose.Card, RenderPurpose.Print];

    private readonly IRenderTokens _tokens;
    private readonly TimeProvider _clock;
    private readonly string? _headerToken;
    private readonly string? _cookieToken;

    // AG0009 is about passing the whole HttpContext into helpers. The accessor is how a per-request service reaches the
    // request, and only the two token values are kept.
#pragma warning disable AG0009
    public RenderSessions(IRenderTokens tokens, TimeProvider clock, IHttpContextAccessor httpContextAccessor)
#pragma warning restore AG0009
    {
        _tokens = tokens;
        _clock = clock;
        var request = httpContextAccessor.HttpContext?.Request;
        _headerToken = request?.Headers[HEADER].ToString();
        _cookieToken = request?.Cookies[COOKIE];
    }

    // The cookie lasts as long as a token of its purpose lives from now. A token issued a little earlier expires first,
    // and every image read checks the token itself again, so an expired token in a live cookie opens nothing.
    public RenderSessionResult Start(string deckId, string? token)
    {
        var purpose = PurposeOf(token, deckId) ?? throw new NotFoundException(DeckReader.NOT_FOUND);
        return new RenderSessionResult(token!, _clock.GetUtcNow().Add(RenderTokens.LifetimeOf(purpose)));
    }

    // True when this request carries a live render token, in the header or the cookie, for this deck.
    public bool AllowsImagesOf(string deckId) =>
        PurposeOf(_headerToken, deckId) != null || PurposeOf(_cookieToken, deckId) != null;

    private RenderPurpose? PurposeOf(string? token, string deckId) =>
        PURPOSES.Where(purpose => _tokens.Validate(token, deckId, purpose)).Cast<RenderPurpose?>().FirstOrDefault();
}
