using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render.Print;

// GET /api/decks/{id}/pdf: the deck as a PDF, one page per slide, for people who can present it. A refusal never
// reaches the renderer: a person who can't present, or an unknown deck, is 404 "Deck not found", and a deck with no
// slides is 400. A renderer that can't run, times out or fails is 503 with the worded error.
[RegisterPerRequest]
public class DeckPdfs(IDeckAccess access, ICurrentActor actor, DeckReader reader, IPdfRenderer pdfs)
{
    public const string NO_SLIDES = "The deck has no slides to print.";
    public const string UNAVAILABLE = "PDF export isn't available right now. Try again in a minute.";
    public const string TIMED_OUT = "Making the PDF took too long. Try again.";
    public const string FAILED = "Couldn't make the PDF: ";

    public async Task<IActionResult> DownloadAsync(string deckId, CancellationToken ct)
    {
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var play = (await reader.ReadForRenderAsync(deckId)).Play;
        if (play.Slides.Count == 0)
        {
            throw new BadRequestException(NO_SLIDES);
        }
        try
        {
            return new FileContentResult(await pdfs.RenderAsync(deckId, ct), "application/pdf") { FileDownloadName = $"{play.Slug}.pdf" };
        }
        catch (RendererUnavailableException)
        {
            return new PdfUnavailableResult(UNAVAILABLE);
        }
        catch (TimeoutException)
        {
            return new PdfUnavailableResult(TIMED_OUT);
        }
        catch (RenderingFailedException e)
        {
            return new PdfUnavailableResult(FAILED + e.Reason);
        }
    }
}
