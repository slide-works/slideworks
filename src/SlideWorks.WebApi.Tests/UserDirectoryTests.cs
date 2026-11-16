using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests;

// IUserDirectory is the identity seam: sign-in stores people through it, so it is checked through the app's own
// container rather than an HTTP route.
[TestFixture]
public class UserDirectoryTests : ApiTestFixture
{
    [Test]
    public async Task Upsert_WhenSameSubjectTwice_ShouldUpdateNotDuplicate()
    {
        var first = await UpsertAsync(TestUser.Sam.GoogleSubject, TestUser.Sam.Email, TestUser.Sam.Name, null);

        var second = await UpsertAsync(TestUser.Sam.GoogleSubject, "sam.rivera@team.example", "Samantha Rivera", "https://pictures.example/sam.png");

        second.Id.ShouldBe(first.Id);
        second.Name.ShouldBe("Samantha Rivera");
        second.PictureUrl.ShouldBe("https://pictures.example/sam.png");
        (await FindByEmailAsync(TestUser.Sam.Email)).ShouldBeNull();
        (await FindByEmailAsync("sam.rivera@team.example"))!.Id.ShouldBe(first.Id);
    }

    [Test]
    public async Task Upsert_ShouldLowerCaseEmail()
    {
        var user = await UpsertAsync(TestUser.Alex.GoogleSubject, "Alex@Team.Example", TestUser.Alex.Name, null);

        user.Email.ShouldBe(TestUser.Alex.Email);
        (await FindByEmailAsync("ALEX@team.example"))!.Id.ShouldBe(user.Id);
    }

    private async Task<Features.Accounts.User> UpsertAsync(string subject, string email, string name, string? pictureUrl)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().UpsertAsync(subject, email, name, pictureUrl);
    }

    private async Task<Features.Accounts.User?> FindByEmailAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().FindByEmailAsync(email);
    }
}
