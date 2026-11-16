using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Gate;

// The Admin policy (S3): only an approved admin passes. Ada is the admin through SLIDEWORKS_ADMIN_EMAIL.
[TestFixture]
public class AdminPolicyTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => GateProbeController.AddTo;

    [Test]
    public async Task Admin_WhenNotAdmin_ShouldReturn403AdminsOnly()
    {
        var response = await Client.GetAsync(GateProbeController.ADMIN_PATH);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Admins only");
    }

    [Test]
    public async Task Admin_WhenPending_ShouldReturnPendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync(GateProbeController.ADMIN_PATH);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public async Task Admin_WhenAdmin_ShouldPass()
    {
        using var ada = ClientFor(TestUser.Ada);

        var response = await ada.GetAsync(GateProbeController.ADMIN_PATH);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
