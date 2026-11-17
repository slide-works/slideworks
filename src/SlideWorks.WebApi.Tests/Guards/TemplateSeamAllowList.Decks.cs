namespace SlideWorks.WebApi.Tests.Guards;

// Features/Decks, Render, Join and Presenting. The last three hold no caller any more.
public static partial class TemplateSeamAllowList
{
    public static IReadOnlyList<string> Decks { get; } =
    [
        "Features/Decks/DeckCommands.cs",
        "Features/Decks/Mcp/CreateDeckTool.cs",
        "Features/Decks/Mcp/ListLayoutsTool.cs",
    ];
}
