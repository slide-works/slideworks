using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render.Print;

// The deck's PDF download. The default (approved person) policy applies.
[Route("api/decks/{id}/pdf")]
public class DeckPdfController(DeckPdfs pdfs) : ApiControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Download(string id, CancellationToken ct) => pdfs.DownloadAsync(id, ct);
}
