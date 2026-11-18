using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// A template's files (T6): fonts, logos, art and custom layouts. Every file is checked by its kind's IAssetCheck
// before it is stored, and a refused file stores nothing. A file's id is the first 32 hex characters of its SHA-256,
// so the same bytes are stored once in a template and a stored file never changes.
public interface ITemplateAssets
{
    // A one-time URL to PUT the file's bytes to. For the template's owner only: anyone else, as for an unknown
    // template, gets NotFoundException. rightsConfirmed is kept only for a font and only from a person.
    Task<AssetUploadStarted> StartUploadAsync(ICurrentActor actor, string templateId, TemplateAssetKind kind, string? fileName, bool rightsConfirmed);

    // The bytes of a started upload. The token is used up by the first call that presents it while it is live, whether
    // the file is then stored or refused. An unknown or expired token is NotFoundException; a used one is refused with
    // upload_used; a file over its kind's limit with file_too_large; a file its check refuses with the check's code.
    // A file that would take the template past TemplateLimits.MAX_STORED_ASSETS or MAX_STORED_BYTES is refused with
    // template_storage_full; bytes the template already holds are never refused for that.
    Task<StoredAsset> CompleteAsync(string token, Stream content, CancellationToken ct);

    // The file, for the template's owner, a person the template is shared with, and anyone who may view a deck on the
    // template. NotFoundException otherwise, and always for a layout, which is never served.
    Task<AssetFile> OpenAsync(string templateId, string assetId);

    // The owner's word, as a person, that they may serve every font the template holds. Answers how many fonts that is.
    // Anyone but the owner gets NotFoundException; an agent gets ForbiddenException, because only a person can confirm.
    Task<int> ConfirmFontRightsAsync(ICurrentActor actor, string templateId);

    // Checks and stores one file for a caller that has already checked the actor owns the template, such as a bundle
    // import. The right to serve a font is left unconfirmed. Answers and refuses as CompleteAsync does.
    Task<StoredAsset> StoreAsync(string templateId, TemplateAssetKind kind, string fileName, Stream content, CancellationToken ct);
}
