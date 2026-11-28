using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Contracts;

// An audience event always carries "type" on the wire, however the hub or a view declares it: SignalR and MVC serialise by
// the runtime type, so JoinAudience's AudienceState result and GuestView.State must not lose the discriminator.
[TestFixture]
public class AudienceDiscriminatorTests : ApiTestFixture
{
    private JsonSerializerOptions HubJson => Factory.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;

    [Test]
    public void AudienceState_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminatorThroughTheHubOptions()
    {
        var state = new AudienceState("7Kq2xYp9LmQa", 3, AudiencePhase.Lobby, null, 1, null, null, null);

        var json = JsonSerializer.Serialize(state, HubJson);

        json.ShouldContain("\"type\":\"audienceState\"");
    }

    [Test]
    public void AudienceState_WhenDeclaredAsObject_ShouldCarryTheDiscriminatorThroughTheHubOptions()
    {
        var json = JsonSerializer.Serialize<object>(new AudienceState("7Kq2xYp9LmQa", 3, AudiencePhase.Lobby, null, 1, null, null, null), HubJson);

        json.ShouldContain("\"type\":\"audienceState\"");
    }

    [Test]
    public void AudienceEnded_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminator()
    {
        JsonSerializer.Serialize(new AudienceEnded("7Kq2xYp9LmQa"), HubJson).ShouldContain("\"type\":\"audienceEnded\"");
    }

    [Test]
    public void JoinedCount_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminator()
    {
        JsonSerializer.Serialize(new JoinedCount("7Kq2xYp9LmQa", 4), HubJson).ShouldContain("\"type\":\"joinedCount\"");
    }

    [Test]
    public void ReadingIn_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminator()
    {
        JsonSerializer.Serialize(new ReadingIn("7Kq2xYp9LmQa", [new ReadingInCount("en", 3)]), HubJson).ShouldContain("\"type\":\"readingIn\"");
    }

    [Test]
    public void QuestionTally_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminator()
    {
        JsonSerializer.Serialize(new QuestionTally("7Kq2xYp9LmQa", "r1", 3, []), HubJson).ShouldContain("\"type\":\"questionTally\"");
    }

    [Test]
    public void QuestionControl_WhenDeclaredAsItsOwnType_ShouldCarryTheDiscriminator()
    {
        var control = new QaControlView(1, false, null, QaSettings.Default, null, [], new QaCounts(0, 0, 0, 0), null, [], ReactionCounts.Zero);

        JsonSerializer.Serialize(new QuestionControl("7Kq2xYp9LmQa", control), HubJson).ShouldContain("\"type\":\"questionControl\"");
    }

    [Test]
    public void ReactionsBurst_WhenDeclaredAsItsOwnType_ShouldCarryTheReactionsDiscriminator()
    {
        JsonSerializer.Serialize(new ReactionsBurst("7Kq2xYp9LmQa", 2, ReactionCounts.Zero), HubJson).ShouldContain("\"type\":\"reactions\"");
    }

    [Test]
    public void AudienceState_ShouldNeverWriteTheDeckId()
    {
        JsonSerializer.Serialize(new AudienceState("7Kq2xYp9LmQa", 3, AudiencePhase.Lobby, null, 1, null, null, null), HubJson).ShouldNotContain("7Kq2xYp9LmQa");
    }
}
