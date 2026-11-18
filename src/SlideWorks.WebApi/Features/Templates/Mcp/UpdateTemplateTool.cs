using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// update_template: reads the draft (its own, or NotFoundException), puts the given fields over its manifest and hands the
// whole manifest to ITemplateDrafts.UpdateAsync, which checks it and refuses a stale revision.
[McpServerToolType]
public sealed class UpdateTemplateTool
{
    [McpServerTool(Name = "update_template")]
    [Description("Change a template draft you own. Give only the manifest fields that change: each top-level key replaces the stored value, and a key set to null is refused. On a template that is already published this changes the draft only; the published versions stay as they are until publish_template. Name files by the asset id upload_asset answered. Pass revision (from get_template or your last result) to refuse the change if someone else changed the draft meanwhile; leave it out to build on the current one. Returns the new revision and the publish checklist.")]
    public Task<CallToolResult> UpdateTemplate(
        ITemplateDrafts drafts,
        ICurrentActor actor,
        [Description("The template's `tpl_…` id, as create_template or list_templates gives it. The short name in the manifest is never an address.")] string template,
        [Description("The manifest fields to change, as one JSON object per the template manifest. Each top-level key replaces the stored value; a key set to null is refused.")] JsonElement manifest,
        [Description("The revision you last read. A different one is refused as stale and changes nothing. Leave it out to use the current one.")] int? revision = null)
        => McpToolErrors.Run(async () =>
        {
            TemplateManifestPatch.Check(manifest);
            var current = await drafts.GetAsync(actor, template);
            var updated = await drafts.UpdateAsync(actor, template, TemplateManifestPatch.Apply(current.Manifest, manifest), revision ?? current.Revision);
            return new UpdateTemplateResult(updated.Id, updated.Revision, TemplateChecks.Of(updated));
        });
}
