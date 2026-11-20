using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Render.Mcp;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render;

// preview_deck (L7, D21): the owner's deck only (anyone else gets "Deck not found"). One Preview token covers every
// slide's URL (one template with {slide} in it, D26), carried in the fragment. The server also renders each slide and
// attaches the PNG, so a caller who can reach this endpoint but not the player still sees the slides.
[RegisterPerRequest]
public class DeckPreviews(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IPublicUrls urls,
    IRenderTokens tokens,
    IRenderEngine engine,
    PreviewInstructions instructions)
{
    public async Task<PreviewDeckOutcome> PreviewAsync(string deckId, IReadOnlyList<int>? slides)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.Id, d.Slug }).SingleAsync();
        var count = await db.Slides.AsNoTracking().CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        var numbers = slides is { Count: > 0 } ? slides.Distinct().ToList() : Enumerable.Range(1, count).ToList();
        if (numbers.Any(n => n < 1 || n > count))
        {
            throw new BadRequestException($"slides: no slide {numbers.First(n => n < 1 || n > count)}");
        }

        var token = tokens.Issue(deck.Id, RenderPurpose.Preview);
        var id = Uri.EscapeDataString(deck.Id);
        var renderUrl = $"{urls.Base}/render/{id}/{{slide}}#token={token}";
        var playerUrl = urls.Deck(deck.Id, deck.Slug);
        var lifetime = RenderTokens.LifetimeOf(RenderPurpose.Preview);
        var images = new List<byte[]>(numbers.Count);
        foreach (var number in numbers)
        {
            try
            {
                images.Add(await engine.RenderSlideAsync(deck.Id, number, CancellationToken.None));
            }
            catch (Exception error) when (error is RendererUnavailableException or TimeoutException or RenderingFailedException)
            {
                break;
            }
        }

        return new PreviewDeckOutcome(
            new PreviewDeckResult(
                renderUrl,
                numbers,
                new PreviewViewport(PreviewInstructions.WIDTH, PreviewInstructions.HEIGHT),
                PreviewInstructions.WAIT_FOR,
                (int)lifetime.TotalSeconds,
                playerUrl,
                $"{urls.Base}/api/cards/{id}.png",
                instructions.Fill(renderUrl, numbers, playerUrl, lifetime),
                images.Count),
            images);
    }
}
