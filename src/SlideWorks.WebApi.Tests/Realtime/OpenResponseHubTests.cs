using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// A joined guest receives wordCloud and openText, drops, and receives the next publish on the new connection.
// The events are put on the audience bus directly, so replacing the publisher classes does not change this.
[TestFixture]
[CancelAfter(60000)]
public class OpenResponseHubTests : ApiTestFixture
{
    private const string CLOUD_ROUND = "6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63";
    private const string TEXT_ROUND = "a3d81c5e-7f2b-4e96-b0c4-1d8e6f9a2b57";

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    [Test]
    public async Task Publish_WhenTheGuestDropsAndRejoins_ShouldReceiveWordCloudAgain()
    {
        var (deck, code) = await GoLiveAsync();
        await using var first = await GuestClient.ConnectAsync(Factory);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await Bus.PublishAsync(Cloud(deck, OpenResponseTarget.Audience, 9, 12, 5));
        var opened = await first.NextAudienceAsync();

        opened.GetProperty("type").GetString().ShouldBe("wordCloud");
        opened.GetProperty("slide").GetInt32().ShouldBe(7);
        opened.GetProperty("phase").GetString().ShouldBe("open");
        opened.GetProperty("round").GetString().ShouldBe(CLOUD_ROUND);
        opened.GetProperty("number").GetInt32().ShouldBe(1);
        opened.GetProperty("revision").GetInt32().ShouldBe(9);
        opened.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        opened.GetProperty("guests").GetInt32().ShouldBe(12);
        opened.GetProperty("words")[0].GetProperty("text").GetString().ShouldBe("Focused");
        opened.GetProperty("words")[0].GetProperty("count").GetInt32().ShouldBe(5);
        opened.GetProperty("words")[0].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        opened.GetRawText().ShouldNotContain(deck, Case.Sensitive);

        await first.Connection.StopAsync();
        first.Connection.State.ShouldBe(HubConnectionState.Disconnected);
        await using var again = await GuestClient.ConnectAsync(Factory, guestId: first.GuestId);
        await again.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var missed = first.NextAudienceAsync();

        await Bus.PublishAsync(Cloud(deck, OpenResponseTarget.Audience, 10, 13, 6));
        var rejoined = await again.NextAudienceAsync();
        var winner = await Task.WhenAny(missed, Task.Delay(500));

        winner.ShouldNotBe(missed);
        rejoined.GetProperty("type").GetString().ShouldBe("wordCloud");
        rejoined.GetProperty("slide").GetInt32().ShouldBe(7);
        rejoined.GetProperty("number").GetInt32().ShouldBe(1);
        rejoined.GetProperty("revision").GetInt32().ShouldBe(10);
        rejoined.GetProperty("guests").GetInt32().ShouldBe(13);
        rejoined.GetProperty("words")[0].GetProperty("count").GetInt32().ShouldBe(6);
        rejoined.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        rejoined.GetRawText().ShouldNotContain(deck, Case.Sensitive);
        _ = missed.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    [Test]
    public async Task Publish_WhenTheGuestDropsAndRejoins_ShouldReceiveOpenTextAgain()
    {
        var (deck, code) = await GoLiveAsync();
        await using var first = await GuestClient.ConnectAsync(Factory);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await Bus.PublishAsync(Text(deck, OpenResponseTarget.Audience, 6, 4, 1, "Smaller stories, so review is quick"));
        var opened = await first.NextAudienceAsync();

        opened.GetProperty("type").GetString().ShouldBe("openText");
        opened.GetProperty("slide").GetInt32().ShouldBe(8);
        opened.GetProperty("phase").GetString().ShouldBe("open");
        opened.GetProperty("round").GetString().ShouldBe(TEXT_ROUND);
        opened.GetProperty("number").GetInt32().ShouldBe(1);
        opened.GetProperty("revision").GetInt32().ShouldBe(6);
        opened.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        opened.GetProperty("guests").GetInt32().ShouldBe(4);
        opened.GetProperty("answers")[0].GetProperty("number").GetInt32().ShouldBe(1);
        opened.GetProperty("answers")[0].GetProperty("text").GetString().ShouldBe("Smaller stories, so review is quick");
        opened.GetProperty("answers")[0].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        opened.GetRawText().ShouldNotContain(deck, Case.Sensitive);

        await first.Connection.StopAsync();
        first.Connection.State.ShouldBe(HubConnectionState.Disconnected);
        await using var again = await GuestClient.ConnectAsync(Factory, guestId: first.GuestId);
        await again.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var missed = first.NextAudienceAsync();

        await Bus.PublishAsync(Text(deck, OpenResponseTarget.Audience, 7, 5, 2, "A shared definition of done"));
        var rejoined = await again.NextAudienceAsync();
        var winner = await Task.WhenAny(missed, Task.Delay(500));

        winner.ShouldNotBe(missed);
        rejoined.GetProperty("type").GetString().ShouldBe("openText");
        rejoined.GetProperty("slide").GetInt32().ShouldBe(8);
        rejoined.GetProperty("number").GetInt32().ShouldBe(1);
        rejoined.GetProperty("revision").GetInt32().ShouldBe(7);
        rejoined.GetProperty("guests").GetInt32().ShouldBe(5);
        rejoined.GetProperty("answers")[0].GetProperty("number").GetInt32().ShouldBe(2);
        rejoined.GetProperty("answers")[0].GetProperty("text").GetString().ShouldBe("A shared definition of done");
        rejoined.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        rejoined.GetRawText().ShouldNotContain(deck, Case.Sensitive);
        _ = missed.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    [Test]
    public async Task Publish_WhenTheConnectionIsNotThePresenter_ShouldNotReceiveThePresenterCopy()
    {
        var (deck, code) = await GoLiveAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await Bus.PublishAsync(Cloud(deck, OpenResponseTarget.Presenter, 3, 7, 1, hidden: true, text: "Secret"));
        await Bus.PublishAsync(Text(deck, OpenResponseTarget.Presenter, 2, 1, 9, "Only the presenter sees this", hidden: true));
        await Bus.PublishAsync(Cloud(deck, OpenResponseTarget.Audience, 4, 8, 5));
        var frame = await guest.NextAudienceAsync();

        frame.GetProperty("type").GetString().ShouldBe("wordCloud");
        frame.GetProperty("slide").GetInt32().ShouldBe(7);
        frame.GetProperty("number").GetInt32().ShouldBe(1);
        frame.GetProperty("revision").GetInt32().ShouldBe(4);
        frame.GetProperty("guests").GetInt32().ShouldBe(8);
        frame.GetProperty("words")[0].GetProperty("count").GetInt32().ShouldBe(5);
        frame.GetProperty("words")[0].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        frame.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        frame.GetRawText().ShouldNotContain("Secret", Case.Sensitive);
        frame.GetRawText().ShouldNotContain("Only the presenter sees this", Case.Sensitive);
        frame.GetRawText().ShouldNotContain(deck, Case.Sensitive);
    }

    private async Task<(string Deck, string Code)> GoLiveAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        using var document = JsonDocument.Parse(body);
        return (deck, document.RootElement.GetProperty("code").GetString()!);
    }

    private static WordCloudTally Cloud(string deck, OpenResponseTarget target, int revision, int guests, int count, bool hidden = false, string text = "Focused")
        => new(deck, target, 7, OpenResponsePhase.Open, CLOUD_ROUND, 1, revision, true, guests, [new CloudWord(text, count, hidden)]);

    private static OpenTextTally Text(string deck, OpenResponseTarget target, int revision, int guests, int number, string text, bool hidden = false)
        => new(deck, target, 8, OpenResponsePhase.Open, TEXT_ROUND, 1, revision, true, guests, [new WallAnswer(number, text, hidden)]);
}
