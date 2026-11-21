using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class CreateDeckTool
{
    public const string NAME = "create_deck";

    [McpServerTool(Name = NAME)]
    [Description("Create a new, private deck owned by the person you act for. Returns its id (deck), its player url, its sharing level, its look (template and palette), and the template's layouts, outline and house style to follow. Pass like with the id of another deck you own to copy its template, version, palette and colours, logo, chrome, draft mark and talk length in this one call. Add slides with add_slide.")]
    public Task<CallToolResult> CreateDeck(
        DeckCommands commands,
        IPublicUrls urls,
        ITemplates templates,
        [Description("The deck's title, up to 120 characters.")] string title,
        [Description("An optional subtitle, up to 200 characters.")] string? subtitle = null,
        [Description("The presenter's name for the footer, up to 80 characters.")] string? author = null,
        [Description("Optional parts, up to 8. Each is a name of up to 40 characters, or {name, short, caption, mark}: short (16) shows on the tracker, caption (24) under the journey stop, and a mark of 1–2 characters such as 'A' makes an unnumbered section, shown on neither.")] JsonElement[]? parts = null,
        [Description("A template's name, as list_templates gives it. Left out, the default template (is_default in list_templates). It sets the layouts, chrome, outline and house style.")] string? template = null,
        [Description("A palette's name, as list_palettes gives it. Left out, the template's own palette, else the default palette (is_default in list_palettes).")] string? palette = null,
        [Description("Custom colours for this deck, without a template: an object of palette token names to # and six hex digits. A colour that fails the contrast check is refused.")] JsonElement? colors = null,
        [Description("The id of another deck you own. Copies its template and version, palette and colours, logo, chrome, draft mark and talk length, not its slides, title or parts. Leave template, palette and colors out.")] string? like = null)
        => McpToolErrors.Run(async () =>
        {
            var deck = await commands.CreateAsync(new CreateDeckRequest(title, subtitle, author, parts, template, palette, colors is null ? null : ReadColors(colors.Value), like), NAME);
            return CreateDeckToolResult.From(deck, urls.Deck(deck.Id, deck.Slug), await templates.ForDeckAsync(deck, CancellationToken.None));
        });

    private static Dictionary<string, string> ReadColors(JsonElement colors)
    {
        if (colors.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new Dictionary<string, string>();
        }

        if (colors.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException("colors: an object of colour token to #rrggbb");
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in colors.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                throw new BadRequestException($"colors.{entry.Name}: a colour is # and six hex digits");
            }

            map[entry.Name] = entry.Value.GetString()!;
        }

        return map;
    }
}
