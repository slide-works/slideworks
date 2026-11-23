using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Contracts;

// Test-only, and only on hosts that add it with AddTo: answers GET with K4 records that carry DeckSharing, ActorKind and
// ActorVia, through the app's real MVC JSON options, so a test reads the enums as they leave over HTTP. No product
// endpoint carries an enum yet (the library, deck page and sharing endpoints come later); those endpoints' own tests take
// over then.
[ApiController]
[Route(PATH)]
public class EnumWireProbeController : ControllerBase
{
    public const string PATH = "test/enum-wire-probe";

    // For ConfigureTestServices: makes this test assembly's controllers part of the app.
    public static void AddTo(IServiceCollection services)
        => services.AddControllers().AddApplicationPart(typeof(EnumWireProbeController).Assembly);

    [HttpGet]
    [ProducesResponseType<EnumWireSample>(StatusCodes.Status200OK)]
    public EnumWireSample Get()
    {
        var entry = new ActivityEntryRow
        {
            DeckId = "7Kq2xYp9LmQa",
            ActorName = "Claude",
            ActorKind = nameof(ActorKind.Agent),
            Via = nameof(ActorVia.Mcp),
            Tool = "set_sharing",
            Summary = "changed sharing",
        };
        return new EnumWireSample(
            new SharingView(DeckSharing.Link, [], "https://slideworks.example/d/7Kq2xYp9LmQa/ship-smaller", SharingLastChange.From(entry, "just now"), new SharingOwnerView("sam@team.example", "Sam Rivera", new AvatarView("SR", "teal", null)), []),
            ActivityRow.From(entry, "just now"));
    }
}
