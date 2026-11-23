using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Sharing;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Sharing.Mcp;

// The level comes in as a string and SharingService (a deck) or TemplateSharing.LevelOf (a template) matches it against the wire
// names, because the SDK binds tool arguments with its own serializer, not the app's camelCase enum converter. viewers is
// typed as a list of email addresses, so McpArgumentCheck refuses anything else as "viewers: must be a list of email
// addresses" before binding. Exactly one of deck and template is given: both are optional in the schema, and the tool
// refuses the other cases, before anything is read, with BOTH_GIVEN and NONE_GIVEN.
[McpServerToolType]
public sealed class SetSharingTool
{
    public const string BOTH_GIVEN = "give deck or template, not both";

    public const string NONE_GIVEN = "deck: required";

    public const string PERSON_AND_VIEWERS = "give person or viewers, not both";

    public const string PERSON_ON_TEMPLATE = "person: only for a deck";

    public const string LEVEL_REQUIRED = "level: required";

    [McpServerTool(Name = SharingService.TOOL, Idempotent = true)]
    [Description("Change who can open a deck or a template you own: \"private\" (only you), \"link\" (anyone with the link, signed in or not; they can view and present a deck, never edit; they can look at a template and, signed in, add it to theirs) or \"named\" (only the people whose emails are in viewers, after they sign in). Give exactly one of deck or template. Give viewers to replace the list; leave it out to keep it. A deck's change shows in its activity. Give person (a full email) with access to add, change or remove one person. Returns the level, the viewers, the people with their access and the share link.")]
    public Task<CallToolResult> SetSharing(
        SharingService sharing,
        ITemplateSharing templateSharing,
        ICurrentActor actor,
        [Description("\"private\", \"link\" or \"named\". Leave it out to keep the level (a deck only).")] string? level = null,
        [Description("The deck's id. Give this or template, not both.")] string? deck = null,
        [Description("The template's id (tpl_…). Give this or deck, not both.")] string? template = null,
        [Description("The named viewers' email addresses, up to 50. They only get in while the level is \"named\".")] [EmailAddress] IReadOnlyList<string?>? viewers = null,
        [Description("One person's full email address, to add or change just them. Give it with access.")] string? person = null,
        [Description("\"read\", \"write\" or \"remove\", for person.")] string? access = null)
        => McpToolErrors.Run(async () =>
        {
            if (deck != null && template != null)
            {
                throw new BadRequestException(BOTH_GIVEN);
            }
            if ((person != null || access != null) && viewers != null)
            {
                throw new BadRequestException(PERSON_AND_VIEWERS);
            }
            if ((person != null || access != null) && template != null)
            {
                throw new BadRequestException(PERSON_ON_TEMPLATE);
            }
            if (template != null)
            {
                var update = new TemplateSharingUpdate(TemplateSharing.LevelOf(level ?? throw new BadRequestException(LEVEL_REQUIRED)), viewers?.Select(email => email ?? "").ToList()!);
                return (object)await templateSharing.SetAsync(actor, template, update);
            }
            var deckId = deck ?? throw new BadRequestException(NONE_GIVEN);
            return person != null || access != null
                ? await sharing.SetPersonFromAgentAsync(deckId, level, person, access)
                : await sharing.SetFromAgentAsync(deckId, level, viewers);
        });
}
