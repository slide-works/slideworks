using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Agoda.IoC.Core;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Render;

// GET /d/{id}/{slug?} (L9, D12): the SPA's index.html, read through the same file provider as the SPA fallback, with
// Open Graph and Twitter card tags added before </head>, so a link preview shows the deck. Only a link-shared deck names
// its title, subtitle, slide count and author; an unknown, private or named-viewer deck gets the generic tags, still
// with 200, so the page itself behaves as the plain fallback would. Every value is HTML-encoded.
[RegisterPerRequest]
public class PlayerPageHtml(
    LinkDecks links,
    DeckReader reader,
    IPublicUrls urls,
    IOptions<StaticFileOptions> staticFiles,
    IWebHostEnvironment environment)
{
    private const string INDEX = "index.html";
    private const string HEAD_END = "</head>";
    private const string SITE = "SlideWorks";
    private const string GENERIC_DESCRIPTION = "A slide deck on SlideWorks.";

    public async Task<IResult> ServeAsync(string deckId)
    {
        var index = (staticFiles.Value.FileProvider ?? environment.WebRootFileProvider).GetFileInfo(INDEX);
        if (!index.Exists)
        {
            return Results.NotFound();
        }
        string html;
        await using (var stream = index.CreateReadStream())
        using (var text = new StreamReader(stream, Encoding.UTF8))
        {
            html = await text.ReadToEndAsync();
        }
        return Results.Content(WithTags(html, await TagsAsync(deckId)), "text/html; charset=utf-8");
    }

    private async Task<string> TagsAsync(string deckId)
    {
        var image = $"{urls.Base}/api/cards/{Uri.EscapeDataString(deckId)}.png";
        if (!await links.IsLinkSharedAsync(deckId))
        {
            return Tags(SITE, GENERIC_DESCRIPTION, image, url: null);
        }
        var deck = await reader.ReadForRenderAsync(deckId);
        var play = deck.Play;
        var count = play.Slides.Count == 1 ? "1 slide" : string.Create(CultureInfo.InvariantCulture, $"{play.Slides.Count} slides");
        var byline = string.IsNullOrWhiteSpace(play.Author) ? count : $"{count} · {play.Author}";
        var description = string.IsNullOrWhiteSpace(deck.Subtitle) ? byline : $"{deck.Subtitle} · {byline}";
        return Tags(play.Title, description, image, urls.Deck(play.Id, play.Slug));
    }

    private static string Tags(string title, string description, string image, string? url)
    {
        var tags = new StringBuilder();
        Meta(tags, "property", "og:site_name", SITE);
        Meta(tags, "property", "og:type", "website");
        Meta(tags, "property", "og:title", title);
        Meta(tags, "property", "og:description", description);
        Meta(tags, "property", "og:image", image);
        Meta(tags, "property", "og:image:width", "1200");
        Meta(tags, "property", "og:image:height", "630");
        if (url != null)
        {
            Meta(tags, "property", "og:url", url);
        }
        Meta(tags, "name", "twitter:card", "summary_large_image");
        return tags.ToString();
    }

    private static void Meta(StringBuilder tags, string attribute, string key, string value)
        => tags.Append(CultureInfo.InvariantCulture, $"    <meta {attribute}=\"{key}\" content=\"{HtmlEncoder.Default.Encode(value)}\" />\n");

    // Before </head>; a page with no </head> (a stand-in bundle) gets them at the top, which browsers put in the head.
    private static string WithTags(string html, string tags)
    {
        var end = html.IndexOf(HEAD_END, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? tags + html : html.Insert(end, tags);
    }
}
