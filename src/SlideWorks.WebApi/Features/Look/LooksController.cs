using Microsoft.AspNetCore.Mvc;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Look;

// The templates the signed-in person may pick, and the palettes (looks.json). The templates are per person: the
// built-ins, their own and the ones shared with them (D20, T4).
[Route("api/looks")]
public class LooksController(ITemplates templates, IPaletteCatalogue palettes, ICurrentActor actor) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<LooksView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<LooksView> Get(CancellationToken ct) => LooksView.From(await templates.ListForAsync(actor, ct), palettes, actor.PresenterId);
}
