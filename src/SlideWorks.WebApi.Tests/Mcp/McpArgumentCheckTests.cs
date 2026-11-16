using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Mcp;

// The tools advertise strict, typed schemas, and McpArgumentCheck turns any call whose arguments don't fit them into a
// tool error naming the argument, for every tool: not the SDK's generic "An error occurred invoking …", and not JSON-RPC
// -32603 for text that isn't valid UTF-16. All on the raw JSON-RPC wire, because the C# client never sends such calls.
[TestFixture]
[CancelAfter(60000)]
public class McpArgumentCheckTests : ApiTestFixture
{
    private const string NOTES_NOT_A_LIST = "notes: must be a list of objects with slide and text";

    // Each case runs against a deck holding one quote slide "A" (revision 1, no notes, private, no viewers).
    [TestCase("set_notes", """{"deck":"{deck}","notes":"Say hi."}""", NOTES_NOT_A_LIST)]
    [TestCase("set_notes", """{"deck":"{deck}","notes":{"slide":1,"text":"Say hi."}}""", NOTES_NOT_A_LIST)]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":"1","text":"Say hi."}]}""", "notes[0].slide: must be a whole number")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":1.5,"text":"Say hi."}]}""", "notes[0].slide: must be a whole number")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"text":"Say hi."}]}""", "notes[0].slide: required")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":1,"text":"Say hi."},{"text":"y"}]}""", "notes[1].slide: required")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":null,"text":"Say hi."}]}""", "notes[0].slide: required")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":1,"text":7}]}""", "notes[0].text: must be text")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":["Say hi."]}""", "notes[0]: must be an object with slide and text")]
    [TestCase("update_slide", """{"deck":"{deck}","slide":"1","revision":1,"slots":{"quote":"B","attribution":"Sam"}}""", "slide: must be a whole number")]
    [TestCase("update_slide", """{"deck":"{deck}","slide":1,"revision":true,"slots":{"quote":"B","attribution":"Sam"}}""", "revision: must be a whole number")]
    [TestCase("move_slide", """{"deck":"{deck}","slide":"1","to":1}""", "slide: must be a whole number")]
    [TestCase("delete_slide", """{"deck":"{deck}","slide":"1"}""", "slide: must be a whole number")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"part":"1"}""", "part: must be a whole number")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"notes":["Say hi."]}""", "notes: must be text")]
    [TestCase("add_slide", """{"deck":"{deck}","slots":{"quote":"B","attribution":"Sam"}}""", "layout: required")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":null,"slots":{"quote":"B","attribution":"Sam"}}""", "layout: required")]
    [TestCase("add_slide", """{"layout":"quote","slots":{"quote":"B","attribution":"Sam"}}""", "deck: required")]
    [TestCase("update_slide", """{"deck":"{deck}","revision":1,"slots":{"quote":"B","attribution":"Sam"}}""", "slide: required")]
    [TestCase("set_sharing", """{"deck":"{deck}"}""", "level: required")]
    [TestCase("set_sharing", """{"deck":"{deck}","level":1}""", "level: must be text")]
    [TestCase("set_sharing", """{"deck":"{deck}","level":"named","viewers":["alex@team.example",2]}""", "viewers: must be a list of email addresses")]
    [TestCase("create_deck", """{}""", "title: required")]
    [TestCase("create_deck", """{"title":null}""", "title: required")]
    [TestCase("create_deck", """{"title":42}""", "title: must be text")]
    [TestCase("create_deck", """{"title":"T","parts":"One"}""", "parts: must be a list of text")]
    [TestCase("create_deck", """{"title":"T","parts":["One",2]}""", "parts: must be a list of text")]
    public async Task Call_WhenArgumentMissingOrMistypedOnTheWire_ShouldReturnToolErrorNamingItAndChangeNothing(string tool, string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        var decks = await DeckCountAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, tool, argumentsJson.Replace("{deck}", deck, StringComparison.Ordinal));

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected, result.GetRawText());
        McpWire.TextOf(result).ShouldContain(expected, Case.Sensitive);
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["A"]);
        Revisions(slides).ShouldBe([1]);
        slides[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("level").GetString().ShouldBe("private");
        sharing.GetProperty("viewers").GetArrayLength().ShouldBe(0);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(2);
        (await DeckCountAsync(agent)).ShouldBe(decks);
    }

    // An escaped lone surrogate (half an emoji) can't become a .NET string, and the SDK reads a call's params before any
    // call-tool filter runs, so without McpArgumentCheck.Unreadable every one of these is JSON-RPC -32603.
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"Ship it \ud83d","attribution":"Sam"}}""", "slots: text is not valid UTF-16")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"\ud83d":"x","quote":"B","attribution":"Sam"}}""", "slots: text is not valid UTF-16")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"notes":"\ude80 lift-off"}""", "notes: text is not valid UTF-16")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"extra":"\ud83d"}""", "extra: text is not valid UTF-16")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"\ud83d":"x"}""", "arguments: text is not valid UTF-16")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":1,"text":"\ud83d\ud83d"}]}""", "notes: text is not valid UTF-16")]
    [TestCase("set_sharing", """{"deck":"{deck}","level":"named","viewers":["alex\ud83d@team.example"]}""", "viewers: text is not valid UTF-16")]
    [TestCase("create_deck", """{"title":"\ud83d"}""", "title: text is not valid UTF-16")]
    public async Task Call_WhenArgumentHasLoneSurrogateOnTheWire_ShouldReturnToolErrorNamingItAndChangeNothing(string tool, string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        var decks = await DeckCountAsync(agent);

        var message = await McpWire.SendToolCallAsync(Factory, agent.Tokens.AccessToken, tool, argumentsJson.Replace("{deck}", deck, StringComparison.Ordinal));

        message.TryGetProperty("result", out var result).ShouldBeTrue(message.GetRawText());
        result.GetProperty("isError").GetBoolean().ShouldBeTrue(message.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected, message.GetRawText());
        McpWire.TextOf(result).ShouldContain(expected, Case.Sensitive);
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["A"]);
        Revisions(slides).ShouldBe([1]);
        slides[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("viewers").GetArrayLength().ShouldBe(0);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(2);
        (await DeckCountAsync(agent)).ShouldBe(decks);
    }

    // The check must not refuse good text: an escaped surrogate pair is a whole emoji, and it is stored as one.
    [Test]
    public async Task Call_WhenTextHasEscapedSurrogatePairOnTheWire_ShouldStoreTheEmoji()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"quote","slots":{"quote":"Ship it 🚀","attribution":"Sam"},"notes":"🚀"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["Ship it \U0001F680"]);
        slides[0].GetProperty("notes").GetString().ShouldBe("\U0001F680");
    }

    // A whole number the schema allows but the parameter's type can't hold reaches the SDK's binder, which fails; that is
    // still a tool error, not the SDK's generic text.
    [Test]
    public async Task Call_WhenWholeNumberTooBigForTheParameterOnTheWire_ShouldReturnToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_notes", $$$"""{"deck":"{{{deck}}}","notes":[{"slide":99999999999,"text":"x"}]}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe("arguments: don't fit the tool's parameters", result.GetRawText());
        (await SlidesAsync(agent, deck))[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // What an agent's model reads: slots and notes required with no default, notes a list of slide/text objects, viewers
    // an optional list of strings.
    [Test]
    public async Task ToolsList_ShouldAdvertiseRequiredAndTypedParameters()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var schemas = await McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken);

        Required(schemas["add_slide"]).ShouldBe(["deck", "layout", "slots"]);
        Required(schemas["update_slide"]).ShouldBe(["deck", "slide", "revision", "slots"]);
        Required(schemas["set_notes"]).ShouldBe(["deck", "notes"]);
        Required(schemas["set_sharing"]).ShouldBe(["deck", "level"]);
        Required(schemas["create_deck"]).ShouldBe(["title"]);
        Property(schemas["add_slide"], "slots").TryGetProperty("default", out _).ShouldBeFalse();
        Property(schemas["update_slide"], "slots").TryGetProperty("default", out _).ShouldBeFalse();
        var notes = Property(schemas["set_notes"], "notes");
        notes.TryGetProperty("default", out _).ShouldBeFalse();
        notes.GetProperty("type").GetRawText().ShouldBe("\"array\"");
        notes.GetProperty("items").GetProperty("type").GetRawText().ShouldBe("\"object\"");
        Required(notes.GetProperty("items")).ShouldBe(["slide", "text"]);
        notes.GetProperty("items").GetProperty("properties").GetProperty("slide").GetProperty("type").GetRawText().ShouldBe("\"integer\"");
        var viewers = Property(schemas["set_sharing"], "viewers");
        viewers.GetProperty("type").GetRawText().ShouldBe("""["array","null"]""");
        viewers.GetProperty("items").GetProperty("type").GetRawText().ShouldBe("""["string","null"]""");
        viewers.GetProperty("format").GetString().ShouldBe("email");
    }

    private static async Task<int> DeckCountAsync(McpTestClient agent)
        => (await OkAsync(agent, "list_decks", new { })).GetProperty("decks").GetArrayLength();

    private static List<string?> Required(JsonElement schema)
        => schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ToList();

    private static JsonElement Property(JsonElement schema, string name)
        => schema.GetProperty("properties").GetProperty(name);
}
