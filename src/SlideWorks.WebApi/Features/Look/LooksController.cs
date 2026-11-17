using Microsoft.AspNetCore.Mvc;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Look;

// The template and palette catalogue (looks.json), the same for every signed-in person and every deck (D20).
[Route("api/looks")]
public class LooksController(ITemplateCatalogue templates, IPaletteCatalogue palettes) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<LooksView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public LooksView Get() => LooksView.From(templates, palettes);
}
