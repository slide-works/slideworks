using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// set_title: renames a deck after it was made, with create_deck's rules for the title, subtitle and author.
[McpServerToolType]
public sealed class SetTitleTool
{
    [McpServerTool(Name = TitleCommands.TOOL)]
    [Description("Rename a deck you can edit: its title, and its subtitle and author when you send them. The title shows on the join page, the phones and the slide footer. The deck's address takes a slug from the new title, and the old address still opens the deck. The change shows in the deck's activity.")]
    public Task<CallToolResult> SetTitle(
        TitleCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The new title, up to 120 characters.")] string title,
        [Description("The new subtitle, up to 200 characters. Leave it out to keep it, or send an empty string to clear it.")] string? subtitle = null,
        [Description("The new author, up to 80 characters. Leave it out to keep it, or send an empty string to clear it.")] string? author = null)
        => McpToolErrors.Run(async () => SetTitleResult.From(await commands.SetAsync(deck, title, subtitle, author, TitleCommands.TOOL)));
}
