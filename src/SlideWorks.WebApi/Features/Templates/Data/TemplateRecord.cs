using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Data;

// An uploaded template (SW-50 T1): its owner, its label and who may use it. Its content is in its versions: version 0 is
// the draft, and published versions count from 1. Never deleted; ArchivedAt hides it.
public class TemplateRecord
{
    public const string ID_PREFIX = "tpl_";

    public const int NAME_MAX = 60;

    // "tpl_" plus 12 characters from the DeckIds alphabet.
    public required string Id { get; set; }

    public Guid OwnerId { get; set; }

    // The label, at most 60 characters.
    public required string Name { get; set; }

    // Who can use the template: only its owner, anyone with the link, or the named viewers. A deck's own words.
    public DeckSharing Access { get; set; }

    // Starts at 1; goes up by 1 on each write of the draft's manifest and on nothing else.
    public long Revision { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}
