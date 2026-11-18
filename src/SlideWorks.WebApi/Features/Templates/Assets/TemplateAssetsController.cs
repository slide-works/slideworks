using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// A template's files (T6). Starting an upload and confirming font rights stay on the default policy, so they need an
// approved person's session: an agent's token works on /mcp only, and no MCP tool confirms the right to serve a font.
// Reading a file is open to anonymous callers, because a deck shared by link draws with its template's files;
// ITemplateAssets decides, and a file the caller may not read is 404, the same as an unknown one. A guest with the
// sw_guest cookie also reads the files of a template whose deck has a live session, as with that deck's images.
[Route("api/templates/{id}/assets")]
[TemplateErrors]
public class TemplateAssetsController(ITemplateAssets assets, ICurrentActor actor) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<AssetUploadStarted>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ObjectResult> Start(string id, StartAssetUploadRequest request)
        => StatusCode(StatusCodes.Status201Created, await assets.StartUploadAsync(actor, id, request.Kind, request.FileName, request.RightsConfirmed));

    [HttpGet("{assetId}")]
    [AllowAnonymous]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, AssetContentTypes.WOFF2, AssetContentTypes.SVG, AssetContentTypes.PNG, AssetContentTypes.WEBP, AssetContentTypes.UNKNOWN)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<AssetFileResult> Get(string id, string assetId) => new(await assets.OpenAsync(id, assetId, GuestCookie.From(Request)));

    [HttpPost("font-rights")]
    [ProducesResponseType<FontRightsConfirmed>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<FontRightsConfirmed> ConfirmFontRights(string id) => new(await assets.ConfirmFontRightsAsync(actor, id));
}
