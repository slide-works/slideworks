using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// publish_template: reads the checklist, and publishes the draft as the next version only when every row allows it. The
// service answers NotFoundException for a template that is not the caller's, so an unknown and a foreign id read the same.
[McpServerToolType]
public sealed class PublishTemplateTool
{
    private const string FONT_RIGHTS = "fontRights";

    [McpServerTool(Name = "publish_template")]
    [Description("Publish a template draft you own as its next version, once every check passes (see the checks create_template, update_template and get_template return). Returns the template, the version number and the editor's url. A failed check is refused with what to fix; font rights can only be confirmed by a person in the editor's Fonts tab, so the answer then names the editor to ask them to open. Decks already on the template keep their version until their owner moves them.")]
    public Task<CallToolResult> PublishTemplate(
        ITemplatePublishing publishing,
        ICurrentActor actor,
        IPublicUrls urls,
        [Description("The template's `tpl_…` id, as create_template or list_templates gives it. A template's name is never an address.")] string template)
        => McpToolErrors.Run(async () =>
        {
            var editor = TemplateUrls.Editor(urls, template);
            var checklist = await publishing.ChecklistAsync(actor, template);
            if (!checklist.CanPublish)
            {
                throw new BadRequestException(CantPublish(checklist.Checklist.Where(row => row.State != TemplateCheckState.Pass).Select(row => (row.Key, Text(row))), editor));
            }
            try
            {
                var published = await publishing.PublishAsync(actor, template);
                return new PublishTemplateResult(template, published.Version, editor);
            }
            catch (TemplateRefusedException refused)
            {
                // The draft changed between the checklist and the publish: the refusal is worded as the checklist's is.
                throw new BadRequestException(CantPublish([(refused.Code == TemplateErrorCodes.RIGHTS_NOT_CONFIRMED ? FONT_RIGHTS : refused.Code, refused.Message)], editor));
            }
        });

    private static string Text(TemplateChecklistRow row) => row.Detail.Length > 0 ? row.Detail : row.Label;

    private static string CantPublish(IEnumerable<(string Key, string Text)> failing, string editor)
    {
        var lines = failing.Select(f => f.Key == FONT_RIGHTS ? $"{f.Text} Ask a person to open {editor}" : f.Text);
        return $"Can't publish yet: {string.Join(" ", lines)}";
    }
}
