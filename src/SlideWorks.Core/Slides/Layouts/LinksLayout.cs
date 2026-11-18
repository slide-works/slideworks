using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class LinksLayout
{
    private const int MAX_LINKS = 6;
    private const int URL_MAX = 500;
    private const int QR_URL_MAX = 200;

    // The list is 1680 px wide, or 1216 px beside the 400 px code and its 64 px gap. A row's text sits
    // after a 150 px kind column and a 76 px gutter and padding: 226 px.
    private const double LIST_WIDTH = 1680;
    private const double LIST_WIDTH_WITH_QR = 1216;
    private const double TEXT_INSET = 226;
    private const double ROW_PADDING = 28;
    private const double TITLE_SIZE = 28;
    private const double TITLE_LINE = 35;
    private const double SOURCE_SIZE = 20;
    private const double SOURCE_LINE = 26;
    private const double SOURCE_GAP = 4;
    private const double ROW_GAP = 12;

    public static LayoutDefinition Definition { get; } = new("links", "A list of 1–6 links, each a kind, a title and a source, with an optional QR code for one address. Titles open the link in the player.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("links", new ListSlot(Obj(
            Opt("kind", new TextSlot(16), "Such as 'Talk' or 'Article'"),
            Req("title", new TextSlot(90), "Drawn as the link"),
            Opt("source", new TextSlot(100)),
            Req("url", new LinkSlot(URL_MAX))), 1, MAX_LINKS)),
        Opt("qr", Obj(
            Req("url", new LinkSlot(QR_URL_MAX), "The address the QR code opens. Short addresses scan best"),
            Opt("caption", new TextSlot(60), "Under the code; defaults to the address without its scheme")),
            "A QR code beside the list for one address"),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        if (validation.ErrorCount > 0)
        {
            return;
        }

        var width = ObjectSlot.IsPresent(slots, "qr", out _) ? LIST_WIDTH_WITH_QR : LIST_WIDTH;
        var links = slots.GetProperty("links");
        var height = (links.GetArrayLength() - 1) * ROW_GAP;
        foreach (var link in links.EnumerateArray())
        {
            height += ROW_PADDING + (SlideFit.Lines(link.GetProperty("title").GetString()!, width - TEXT_INSET, TITLE_SIZE, validation.Widths.Head) * TITLE_LINE);
            if (ObjectSlot.IsPresent(link, "source", out var source))
            {
                height += SOURCE_GAP + (SlideFit.Lines(source.GetString()!, width - TEXT_INSET, SOURCE_SIZE, validation.Widths.Body) * SOURCE_LINE);
            }
        }

        SlideFit.Check(height, $"{path}.links", validation);
    }
}
