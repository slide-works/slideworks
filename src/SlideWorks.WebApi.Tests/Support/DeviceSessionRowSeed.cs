using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Features.DeviceSessions.Data;

namespace SlideWorks.WebApi.Tests.Support;

// A signed-in phone for tests (K5): signs the person in through TestPeople, then creates a live device session for them
// through the real IDeviceSessions, as the exchange does, and returns it with its secret for an Authorization bearer
// header or an access_token query. Left out of the acceptance project's Support compile.
public sealed class DeviceSessionRowSeed(IServiceProvider services)
{
    public const string DEVICE_NAME = "Test phone";

    public async Task<(DeviceSession Session, string Secret)> CreateAsync(TestUser person, string deviceName = DEVICE_NAME)
    {
        await using var scope = services.CreateAsyncScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        return await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().CreateAsync(user.Id, deviceName);
    }
}
