using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Infrastructure;

// Shared HTTP-layer plumbing so every action stays a one-line delegation to a
// service: exception→status mapping via the filter, response helpers here.
[ApiController]
[Produces("application/json")]
[ApiExceptionFilter]
public abstract class ApiControllerBase : ControllerBase
{
    protected CreatedAtActionResult CreatedAtGet<T>(T viewModel) where T : IHasId
        => CreatedAtAction("Get", new { id = viewModel.Id }, viewModel);

    protected async Task<NoContentResult> NoContentAfter(Task task)
    {
        await task;
        return NoContent();
    }
}
