using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
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
    public async Task UpsertAsync_WhenNameSetByPerson_ShouldKeepName()
    {
        var person = TestUser.NewApproved();
        var first = await UpsertAsync(person.GoogleSubject, person.Email, person.Name, null);
        await SetAsync(first.Id, u => u.NameSetByPerson = true);

        var second = await UpsertAsync(person.GoogleSubject, person.Email, "Google Renamed", "https://pictures.example/new.png");

        second.Name.ShouldBe(person.Name);
        second.PictureUrl.ShouldBe("https://pictures.example/new.png");
    }

    [Test]
    public async Task UpsertAsync_WhenNameNotSet_ShouldTakeGoogleName()
    {
        var person = TestUser.NewApproved();
        await UpsertAsync(person.GoogleSubject, person.Email, person.Name, null);

        var second = await UpsertAsync(person.GoogleSubject, person.Email, "Google Renamed", null);

        second.Name.ShouldBe("Google Renamed");
    }

    [Test]
    public async Task UpsertAsync_WhenProfileFieldsSet_ShouldLeaveThemAlone()
    {
        var person = TestUser.NewApproved();
        var first = await UpsertAsync(person.GoogleSubject, person.Email, person.Name, null);
        await SetAsync(first.Id, u =>
        {
            u.AvatarColour = "pink";
            u.PhotoId = "abc123";
            u.PhotoContentType = "image/png";
            u.TimeZone = "Europe/Paris";
        });

        var second = await UpsertAsync(person.GoogleSubject, person.Email, person.Name, "https://pictures.example/g.png");

        second.AvatarColour.ShouldBe("pink");
        second.PhotoId.ShouldBe("abc123");
        second.PhotoContentType.ShouldBe("image/png");
        second.TimeZone.ShouldBe("Europe/Paris");
    }

    [Test]
    public async Task Upsert_ShouldLowerCaseEmail()
    {
        var user = await UpsertAsync(TestUser.Alex.GoogleSubject, "Alex@Team.Example", TestUser.Alex.Name, null);

        user.Email.ShouldBe(TestUser.Alex.Email);
        (await FindByEmailAsync("ALEX@team.example"))!.Id.ShouldBe(user.Id);
    }

    [Test]
    public async Task Upsert_WhenFirstSignInsForOneSubjectRace_ShouldStoreOnePerson()
    {
        // SQLite's async calls complete synchronously, so each upsert runs on its own thread, released together, to
        // make the find-then-insert steps interleave.
        using var start = new Barrier(4);
        var upserts = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait();
            return UpsertAsync(TestUser.Mira.GoogleSubject, TestUser.Mira.Email, TestUser.Mira.Name, null);
        }));

        var users = await Task.WhenAll(upserts);

        users.Select(u => u.Id).Distinct().Count().ShouldBe(1);
        (await FindByEmailAsync(TestUser.Mira.Email))!.Id.ShouldBe(users[0].Id);
    }

    private async Task<Features.Accounts.User> UpsertAsync(string subject, string email, string name, string? pictureUrl)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().UpsertAsync(subject, email, name, pictureUrl);
    }

    // Sets profile fields the way a later profile task will, to prove a sign-in leaves them alone.
    private async Task SetAsync(Guid id, Action<Features.Accounts.User> change)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(await db.Users.SingleAsync(u => u.Id == id));
        await db.SaveChangesAsync();
    }

    private async Task<Features.Accounts.User?> FindByEmailAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().FindByEmailAsync(email);
    }
}
