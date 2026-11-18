using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// get_template: the editor view of the caller's own template, with its sharing level and deck count from the caller's row
// in the templates list. A template that is not the caller's is NotFoundException, as an unknown id is.
[McpServerToolType]
public sealed class GetTemplateTool
{
    [McpServerTool(Name = "get_template", ReadOnly = true)]
    [Description("Read a template you own: its name, status (draft, published or archived), revision, latest published version (null while only a draft), the manifest, the files uploaded to it (asset id, kind, file name, size, whether a person confirmed the rights), the publish checklist, who it is shared with, how many decks use it and the editor's url. Pass the revision back to update_template.")]
    public Task<CallToolResult> GetTemplate(
        ITemplateDrafts drafts,
        ICurrentActor actor,
        IPublicUrls urls,
        [Description("The template's `tpl_…` id, as create_template or list_templates gives it. The short name in the manifest is never an address.")] string template)
        => McpToolErrors.Run(async () =>
        {
            var view = await drafts.GetAsync(actor, template);
            var row = (await drafts.ListAsync(actor)).Mine.FirstOrDefault(r => r.Id == view.Id)
                ?? throw new NotFoundException($"No template '{template}'.");
            var files = view.Assets.Select(a => new TemplateFileInfo(a.AssetId, Wire(a.Kind), a.FileName, a.Bytes, a.RightsConfirmed));
            return new GetTemplateResult(
                view.Id,
                view.Name,
                Wire(view.Status),
                view.Revision,
                view.Version == 0 ? null : view.Version,
                view.Manifest,
                [.. files],
                TemplateChecks.Of(view),
                row.Sharing,
                row.DeckCount,
                TemplateUrls.Editor(urls, view.Id));
        });

    private static string Wire(Enum value) => char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..];
}
