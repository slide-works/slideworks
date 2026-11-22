using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Support.ApiOnly;

// One raw-wire case per MCP tool, grouped by feature folder. Add a case here when a tool is added; McpWireCoverageTests compares this map with tools/list only and fails until they agree.
public static class McpWireCases
{
    private const string QUOTE = """{"quote":"Ship it","attribution":"Sam Rivera"}""";

    // Tools whose case asserts a specific error, with the reason each cannot succeed. At most 5 (a decision the owner can overturn); each needs a reason.
    public static IReadOnlyDictionary<string, string> ExpectedErrorOnly { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // None: every tool succeeds against seeded state, a live deck and a draft template.
    };

    public static IReadOnlyDictionary<string, McpWireCase> All { get; } = new Dictionary<string, McpWireCase>(StringComparer.Ordinal)
    {
        // Decks
        ["create_deck"] = Plain(_ => """{"title":"Wire check","parts":["One","Two"]}"""),
        ["get_deck"] = Plain(c => Json(new { deck = c.Deck })),
        ["get_deck_url"] = Plain(c => Json(new { deck = c.Deck, slide = 2, step = 0 })),
        ["list_decks"] = Plain(_ => "{}"),
        ["list_blocks"] = Plain(_ => "{}"),
        ["list_layouts"] = Plain(_ => "{}"),

        // Slides
        ["add_slide"] = Plain(c => $$$"""{"deck":"{{{c.Deck}}}","layout":"quote","surface":"dark","slots":{{{QUOTE}}}}"""),
        ["add_slides"] = Plain(c => $$$"""{"deck":"{{{c.Deck}}}","slides":[{"layout":"quote","surface":"dark","slots":{{{QUOTE}}}}],"position":"end"}""", StoredEverySlide),
        ["update_slide"] = Plain(async c => $$$"""{"deck":"{{{c.Deck}}}","slide":1,"revision":{{{await c.RevisionOfSlideAsync(1)}}},"surface":"dark","slots":{{{QUOTE}}}}"""),
        ["move_slide"] = Plain(c => Json(new { deck = c.Deck, slide = 1, to = 2 })),
        ["delete_slide"] = Plain(c => Json(new { deck = c.Deck, slide = 1 })),
        ["set_notes"] = Plain(c => Json(new { deck = c.Deck, notes = new[] { new { slide = 1, text = "Open with the story." } } })),
        ["set_parts"] = Plain(c => Json(new { deck = c.Deck, parts = new[] { "Open", "Close" } })),
        ["validate_slide"] = Plain(c => $$"""{"deck":"{{c.Deck}}","layout":"quote","slots":{{QUOTE}},"surface":"dark"}"""),

        // Look
        ["list_palettes"] = Plain(_ => "{}"),
        ["list_templates"] = Plain(_ => "{}"),
        ["set_look"] = Plain(c => $$$"""{"deck":"{{{c.Deck}}}","draft":true,"chrome":{"footer":"author","topBar":"none"}}"""),

        // Render and checks
        ["preview_deck"] = Plain(c => Json(new { deck = c.Deck, slides = new[] { 1 } })),
        ["check_deck"] = Plain(c => Json(new { deck = c.Deck, slides = new[] { 1 } })),

        // Sharing and presenting
        ["set_sharing"] = Plain(c => Json(new { deck = c.Deck, level = "named", viewers = new[] { "alex@team.example" } })),
        ["set_talk_length"] = Plain(c => Json(new { deck = c.Deck, minutes = 20 })),

        // Polls
        ["add_poll_option"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE, text = "Over 500 lines" })),
        ["update_poll"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE, pick = "upTo", max = 2, results = "afterAnswer", opens = "manual" })),
        ["get_poll_results"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE })),
        ["open_poll"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE })),
        ["close_poll"] = Plain(async c =>
        {
            await c.SetUpAsync("open_poll", new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE });
            return Json(new { deck = c.Deck, slide = McpWireContext.POLL_SLIDE });
        }),

        // Open responses
        ["open_word_cloud"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.WORD_CLOUD_SLIDE })),
        ["close_word_cloud"] = Plain(async c =>
        {
            await c.SetUpAsync("open_word_cloud", new { deck = c.Deck, slide = McpWireContext.WORD_CLOUD_SLIDE });
            return Json(new { deck = c.Deck, slide = McpWireContext.WORD_CLOUD_SLIDE });
        }),
        ["open_open_text"] = Plain(c => Json(new { deck = c.Deck, slide = McpWireContext.OPEN_TEXT_SLIDE })),
        ["close_open_text"] = Plain(async c =>
        {
            await c.SetUpAsync("open_open_text", new { deck = c.Deck, slide = McpWireContext.OPEN_TEXT_SLIDE });
            return Json(new { deck = c.Deck, slide = McpWireContext.OPEN_TEXT_SLIDE });
        }),

        // Q&A
        ["update_qa"] = Plain(c => Json(new { deck = c.Deck, questions = "closed", show_on = "qaSlide", review = true })),
        ["get_questions"] = Plain(c => Json(new { deck = c.Deck, state = "open" })),

        // Images
        ["upload_image"] = Plain(c => Json(new { deck = c.Deck, name = "map.png", contentType = "image/png" })),
        ["upload_images"] = Plain(c => Json(new { deck = c.Deck, files = new[] { new { name = "a.png", contentType = "image/png" }, new { name = "b.webp", contentType = "image/webp" } } })),

        // Templates
        ["create_template"] = Plain(_ => """{"name":"Offsite","from":"workshop","manifest":{"purpose":"Offsites."}}"""),
        ["get_template"] = Plain(c => Json(new { template = c.Template })),
        ["update_template"] = Plain(c => Json(new { template = c.Template, manifest = new { purpose = "Changed." } })),
        ["upload_asset"] = Plain(c => Json(new { template = c.Template, kind = "font", filename = "brand-sans.woff2" })),
        ["publish_template"] = Plain(c => Json(new { template = c.Template })),
    };

    // add_slides answers a success object even when a slide was left out; each entry says ok and, if not, why.
    private static string? StoredEverySlide(JsonElement result)
    {
        var slides = result.GetProperty("slides").EnumerateArray().ToList();
        if (slides.Count == 0)
        {
            return "no slide entries returned";
        }

        var left = slides.FirstOrDefault(s => !s.GetProperty("ok").GetBoolean());
        return left.ValueKind == JsonValueKind.Undefined ? null : $"slide was rejected: {left.GetRawText()}";
    }

    private static McpWireCase Plain(Func<McpWireContext, string> arguments, Func<JsonElement, string?>? resultFailure = null) => new(c => Task.FromResult(arguments(c)), ResultFailure: resultFailure);

    private static McpWireCase Plain(Func<McpWireContext, Task<string>> arguments) => new(arguments);

    private static string Json(object arguments) => JsonSerializer.Serialize(arguments);
}
