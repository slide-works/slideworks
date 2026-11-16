using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class CreateDeckTool
{
    public const string NAME = "create_deck";

    [McpServerTool(Name = NAME)]
    [Description("Create a new, private deck owned by the person you act for. Returns its id (deck), its player url and its sharing level. Add slides with add_slide.")]
    public Task<CallToolResult> CreateDeck(
        DeckCommands commands,
        IPublicUrls urls,
        [Description("The deck's title, up to 120 characters.")] string title,
        [Description("An optional subtitle, up to 200 characters.")] string? subtitle = null,
        [Description("The presenter's name for the footer, up to 80 characters.")] string? author = null,
        [Description("Optional part names, up to 8 of 40 characters each. Each slide's part is a 1-based index into this list; the part tracker, dividers and footer show them.")] string[]? parts = null)
        => McpToolErrors.Run(async () =>
        {
            var deck = await commands.CreateAsync(new CreateDeckRequest(title, subtitle, author, parts), NAME);
            return CreateDeckToolResult.From(deck, urls.Deck(deck.Id, deck.Slug));
        });
}
