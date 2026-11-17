using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The "Update my decks" route over FakeUpdateMyDecks: the wire shape, that the template id and the caller reach the seam,
// and who is turned away before it. The app, and so the fake, is shared by the fixture: it is reset after each test.
[TestFixture]
public class UpdateMyDecksRouteTests : ApiTestFixture
{
    private const string TEMPLATE = "tpl_northwind0001";
    private const string ROUTE = $"/api/templates/{TEMPLATE}/update-my-decks";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeUpdateMyDecks.Register;

    private FakeUpdateMyDecks Decks => Factory.Services.GetRequiredService<FakeUpdateMyDecks>();

    [TearDown]
    public void ResetTheFake() => Decks.Reset();

    [Test]
    public async Task Update_WhenPosted_ShouldAnswerACount()
    {
        Decks.Answer(3);

        var response = await Client.PostAsync(ROUTE, content: null);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["count"]);
        body.GetProperty("count").ValueKind.ShouldBe(JsonValueKind.Number);
        body.GetProperty("count").GetInt32().ShouldBe(3);
        Decks.Calls().ShouldBe([$"Run {TEMPLATE} {TestUser.Sam.Email}"]);
    }

    [Test]
    public async Task Update_WhenSignedOut_ShouldBe401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsync(ROUTE, content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Decks.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Update_WhenPending_ShouldBe403()
    {
        using var pat = ClientFor(TestUser.NewPending());

        var response = await pat.PostAsync(ROUTE, content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Decks.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Update_WhenTheSeamRefuses_ShouldAnswerTheMappedStatus()
    {
        Decks.Refuse(new NotFoundException("Template not found"));

        var response = await Client.PostAsync(ROUTE, content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Decks.Calls().ShouldBeEmpty();
    }
}
