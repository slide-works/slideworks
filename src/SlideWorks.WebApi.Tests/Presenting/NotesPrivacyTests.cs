using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// P9: notes reach only the owner, through the presenter view and the deck page. The play view and every
// presentationState carry no "notes" key at all, even for the owner and even when every slide has notes.
[TestFixture]
[CancelAfter(30000)]
public class NotesPrivacyTests : ApiTestFixture
{
    [Test]
    public async Task GetPlay_WhenSlidesHaveNotes_ShouldCarryNoNotesKey()
    {
        var deck = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "link");
        using var anonymous = ClientFor(TestUser.Anonymous);

        foreach (var client in new[] { Client, anonymous })
        {
            var response = await client.GetAsync($"/api/play/{deck}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var play = await response.Content.ReadFromJsonAsync<JsonElement>();
            PresentationStates.PropertyNames(play).ShouldNotContain("notes");
        }
    }

    [Test]
    public async Task PresentationState_WhenJoinedAndPushed_ShouldCarryNoNotesKey()
    {
        var deck = await OwnDeckAsync();
        await using var owner = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);

        var joined = await owner.JoinAsync(deck, "k-1");
        var pushed = await owner.NextAsync();
        var updated = await owner.UpdateAsync(deck, 1, 0);

        PresentationStates.PropertyNames(joined).ShouldNotContain("notes");
        PresentationStates.PropertyNames(pushed).ShouldNotContain("notes");
        PresentationStates.PropertyNames(updated).ShouldNotContain("notes");
    }

    private async Task<string> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return await PresentingSeed.CreateDeckAsync(agent);
    }
}
