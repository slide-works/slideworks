using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// create_template: ITemplateDrafts.CreateAsync, then, when a manifest is given, one UpdateAsync with it put over the copy.
// An unknown built-in answers with the names that exist.
[McpServerToolType]
public sealed class CreateTemplateTool
{
    [McpServerTool(Name = "create_template")]
    [Description("Start a new draft template for the person you act for, copied from a built-in template (its layouts, chrome, outline and house style). Give an optional manifest of the fields to change over the copy. Files (fonts, logos, art) go up with upload_asset and are then named in the manifest by the asset id the upload answers; a person must confirm the rights of every font before it can be published. Returns the template's id (tpl_…), its status, its revision, the editor's url and the publish checklist. Next: refine it with update_template, then publish_template.")]
    public Task<CallToolResult> CreateTemplate(
        ITemplateDrafts drafts,
        ITemplatePublishing publishing,
        ICurrentActor actor,
        IPublicUrls urls,
        [Description("The new template's name, shown to people (up to 60 characters). It is a label, never an address: use the returned `tpl_…` id to name the template in other tools.")] string name,
        [Description("The name of the built-in template to copy, as list_templates gives it.")] string from,
        [Description("Optional manifest fields that replace the copy's, as one JSON object per the template manifest (for example { \"purpose\": \"…\", \"palette\": {…} }). Each top-level key replaces the copied value; a key set to null is refused. Name files by the asset id upload_asset answered.")] JsonElement? manifest = null)
        => McpToolErrors.Run(async () =>
        {
            if (manifest is { } patch)
            {
                TemplateManifestPatch.Check(patch);
            }
            var created = await CreateAsync(drafts, actor, from, name);
            TemplateEditorView view;
            try
            {
                view = await drafts.GetAsync(actor, created.Id);
                if (manifest is { } change)
                {
                    view = await drafts.UpdateAsync(actor, created.Id, TemplateManifestPatch.Apply(view.Manifest, change), view.Revision);
                }
            }
            catch
            {
                // A refused create changes nothing: the draft already made is archived, so it does not count toward the person's cap.
                await ArchiveQuietlyAsync(publishing, actor, created.Id);
                throw;
            }
            return new CreateTemplateResult(view.Id, "draft", view.Revision, TemplateUrls.Editor(urls, view.Id), TemplateChecks.Of(view));
        });

    private static async Task ArchiveQuietlyAsync(ITemplatePublishing publishing, ICurrentActor actor, string id)
    {
        try
        {
            await publishing.ArchiveAsync(actor, id);
        }
        catch
        {
            // The refusal the agent is about to read matters more than a failed clean-up.
        }
    }

    private static async Task<TemplateCreated> CreateAsync(ITemplateDrafts drafts, ICurrentActor actor, string from, string name)
    {
        try
        {
            return await drafts.CreateAsync(actor, from, name);
        }
        catch (NotFoundException missing)
        {
            var builtIns = (await drafts.ListAsync(actor)).BuiltIn.Select(row => row.Id);
            throw new NotFoundException($"{missing.Message} The built-in templates are: {string.Join(", ", builtIns)}.");
        }
    }
}
