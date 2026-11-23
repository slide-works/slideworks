using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mail;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// The share email end to end: a share through the public PUT, the worker on its in-process queue, and the FakeMailSender
// that records what would have been sent. The queue is first in, first out and one loop reads it, so a test that expects NO
// email shares once more to a barrier address and waits for that mail: when it arrives, everything queued before it has
// been handled.
[TestFixture]
public partial class ShareEmailTests : ApiTestFixture
{
    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(20);

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeMailSender.AddTo(services);
        services.AddSingleton<IMailSender>(provider => new SignallingMailSender(provider.GetRequiredService<FakeMailSender>()));
    };

    [Test]
    public async Task Share_WhenNewPerson_ShouldSendOneEmailWithDeckLink()
    {
        var recipient = NewAddress();
        var deck = await NewDeckAsync(Client, "Ship smaller");

        await SharingApi.PutOkAsync(Client, deck.Id, "named", recipient);
        var mail = Single(await WaitForMailAsync(recipient, 1));

        mail.Subject.ShouldBe("Sam Rivera shared a deck with you");
        mail.Html.ShouldContain("Sam Rivera (sam@team.example)");
        mail.Html.ShouldContain("Ship smaller");
        mail.Text.ShouldContain("Sam Rivera (sam@team.example)");
        using var scope = Factory.Services.CreateScope();
        var deckUrl = scope.ServiceProvider.GetRequiredService<IPublicUrls>().Deck(deck.Id, deck.Slug);
        var unsubscribe = scope.ServiceProvider.GetRequiredService<IUnsubscribeLinks>().For(recipient);
        mail.UnsubscribeUrl.ShouldBe(unsubscribe);
        HrefRegex().Matches(mail.Html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ShouldBe([deckUrl, unsubscribe]);
        UrlRegex().Matches(mail.Text).Select(m => m.Value).ShouldBe([deckUrl, unsubscribe]);
        mail.Subject.ShouldNotContain("view", Case.Insensitive);
        mail.Text.ShouldNotContain("edit", Case.Insensitive);
    }

    [Test]
    public async Task Share_WhenNameHasHtml_ShouldEscapeAndCap()
    {
        var sharer = TestUser.NewApproved();
        using var client = ClientFor(sharer);
        var deck = await NewDeckAsync(client);
        await RenameDeckAsync(deck.Id, new string('T', 200));
        await RenameSharerAsync(sharer, "<script>alert(1)</script>" + new string('N', 100));
        var recipient = NewAddress();

        await SharingApi.PutOkAsync(client, deck.Id, "named", recipient);
        var mail = Single(await WaitForMailAsync(recipient, 1));

        mail.Html.ShouldNotContain("<script>");
        mail.Html.ShouldContain("&lt;script&gt;");
        var name = ("<script>alert(1)</script>" + new string('N', 100))[..60];
        mail.Subject.ShouldBe($"{name} shared a deck with you");
        mail.Html.ShouldContain(new string('T', 80));
        mail.Html.ShouldNotContain(new string('T', 81));
        mail.Text.ShouldNotContain(new string('T', 81));
    }

    [Test]
    public async Task Share_WhenNameHasLineBreaksOrBidi_ShouldStripThemFromSubjectAndBody()
    {
        var sharer = TestUser.NewApproved();
        using var client = ClientFor(sharer);
        var title = "Q" + char.ConvertFromUtf32(0x0D) + char.ConvertFromUtf32(0x0A) + char.ConvertFromUtf32(0x07) + char.ConvertFromUtf32(0x202E) + "4";
        var deck = await NewDeckAsync(client, "Deck");
        await RenameSharerAsync(sharer, "Eve" + char.ConvertFromUtf32(0x0D) + char.ConvertFromUtf32(0x0A) + "Bcc: x@y.example" + char.ConvertFromUtf32(0x00) + char.ConvertFromUtf32(0x202E) + char.ConvertFromUtf32(0x2066) + "z");
        await RenameDeckAsync(deck.Id, title);
        var recipient = NewAddress();

        await SharingApi.PutOkAsync(client, deck.Id, "named", recipient);
        var mail = Single(await WaitForMailAsync(recipient, 1));

        mail.Subject.ShouldBe("EveBcc: x@y.examplez shared a deck with you");
        foreach (var text in new[] { mail.Subject, mail.Text, mail.Html })
        {
            text.Any(c => c is '\r' or '\u0007' or '\0' or '‮' or '⁦').ShouldBeFalse();
        }
        mail.Subject.ShouldNotContain("\n");
        mail.Html.ShouldContain(">Q4<");
    }

    [Test]
    public async Task Share_WhenSharerPending_ShouldSendNothing()
    {
        var recipient = NewAddress();
        var deck = await NewDeckAsync(Client);
        using var pending = ClientFor(TestUser.NewPending());

        var response = await SharingApi.PutAsync(pending, deck.Id, new { level = "named", viewers = new[] { recipient } });
        await BarrierAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Mail.SentTo(recipient).ShouldBeEmpty();
    }

    [Test]
    public async Task Share_WhenSuppressed_ShouldSendNothing()
    {
        var recipient = NewAddress();
        var deck = await NewDeckAsync(Client);
        // The unsubscribe route is not built yet, so the address is suppressed through the seam the route will use.
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEmailSuppressions>().AddAsync(recipient);
        }

        await SharingApi.PutOkAsync(Client, deck.Id, "named", recipient);
        await BarrierAsync();

        Mail.SentTo(recipient).ShouldBeEmpty();
    }

    [Test]
    public async Task Share_WhenFourthEmailToAddressToday_ShouldNotSend()
    {
        var recipient = NewAddress();
        foreach (var sharer in new[] { TestUser.NewApproved(), TestUser.NewApproved(), TestUser.NewApproved() })
        {
            using var client = ClientFor(sharer);
            var deck = await NewDeckAsync(client);
            await SharingApi.PutOkAsync(client, deck.Id, "named", recipient);
        }
        await WaitForMailAsync(recipient, 3);
        using var fourth = ClientFor(TestUser.NewApproved());
        var fourthDeck = await NewDeckAsync(fourth);

        await SharingApi.PutOkAsync(fourth, fourthDeck.Id, "named", recipient);
        await BarrierAsync();

        Mail.SentTo(recipient).Count.ShouldBe(3);
    }

    [Test]
    public async Task Share_WhenAccessChangedOrRemoved_ShouldSendNothing()
    {
        var recipient = NewAddress();
        var deck = await NewDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck.Id, "named", recipient);
        await WaitForMailAsync(recipient, 1);

        await SharingApi.PutAsync(Client, deck.Id, new { level = "named", people = new[] { new { email = recipient, access = "write" } } });
        await SharingApi.PutAsync(Client, deck.Id, new { level = "link", people = new[] { new { email = recipient, access = "read" } } });
        await SharingApi.PutAsync(Client, deck.Id, new { level = "named", viewers = Array.Empty<string>() });
        await BarrierAsync();

        Mail.SentTo(recipient).Count.ShouldBe(1);
    }

    private sealed record NewDeck(string Id, string Slug);

    private static async Task<NewDeck> NewDeckAsync(HttpClient owner, string title = "Ship smaller")
    {
        var response = await owner.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new NewDeck(body.GetProperty("id").GetString()!, body.GetProperty("slug").GetString()!);
    }

    private FakeMailSender Mail => Factory.Services.GetRequiredService<FakeMailSender>();

    private static string NewAddress() => $"r-{Guid.NewGuid():N}@recipient.example";

    private static MailMessageModel Single(IReadOnlyList<MailMessageModel> mail)
    {
        mail.Count.ShouldBe(1);
        return mail[0];
    }

    // Waits up to WAIT for the address to have count mails, then returns them. Fails with what arrived.
    private async Task<IReadOnlyList<MailMessageModel>> WaitForMailAsync(string address, int count)
    {
        var sender = (SignallingMailSender)Factory.Services.GetRequiredService<IMailSender>();
        await sender.WaitUntilAsync(() => Mail.SentTo(address).Count >= count, WAIT);
        Mail.SentTo(address).Count.ShouldBe(count);
        return Mail.SentTo(address);
    }

    // Shares a fresh deck to a fresh address as Sam and waits for its mail: the queue is in order, so earlier notices are done.
    private async Task BarrierAsync()
    {
        var address = NewAddress();
        var deck = await NewDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck.Id, "named", address);
        await WaitForMailAsync(address, 1);
    }

    // There is no route to set a display name or a title to hostile text yet, so these tests write it where the profile will.
    private async Task RenameSharerAsync(TestUser sharer, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == sharer.Email);
        user.Name = name;
        user.NameSetByPerson = true;
        await db.SaveChangesAsync();
    }

    private async Task RenameDeckAsync(string deckId, string title)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Decks.Single(d => d.Id == deckId).Title = title;
        await db.SaveChangesAsync();
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlRegex();
}
