using SlideWorks.Core;
using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Drafts;

// One row of the templates page (templates.json), for an uploaded template or a built-in one.
public static class TemplateRows
{
    public const string ANY_PALETTE = "any";

    // The template's own draft or latest version supplies the facts.
    public static TemplateRowView OfManifest(TemplateRecord record, TemplateManifest manifest, int version, int deckCount, DateTimeOffset now)
        => Uploaded(
            record, manifest.Name, manifest.Purpose, manifest.Type.Head, manifest.Type.Body,
            manifest.Palette is null ? ANY_PALETTE : manifest.Label, manifest.LockPalette, version, deckCount, now, null);

    // A template shared with the caller: its facts are the latest published version's, as ITemplates resolved it.
    public static TemplateRowView OfResolved(TemplateRecord record, ResolvedTemplate resolved, IPaletteCatalogue palettes, int deckCount, DateTimeOffset now)
    {
        var template = resolved.Template;
        return Uploaded(
            record, template.Name, template.Purpose, template.Type.Head, template.Type.Body, PaletteLabel(template, palettes), template.LockPalette,
            resolved.Version ?? resolved.LatestVersion ?? 0, deckCount, now, resolved.OwnerName);
    }

    private static TemplateRowView Uploaded(
        TemplateRecord record, string shortName, string purpose, string head, string body, string palette, bool paletteLocked,
        int version, int deckCount, DateTimeOffset now, string? owner)
        => new(
            record.Id, record.Name, shortName, purpose, $"{head} / {body}", palette, paletteLocked, version,
            StatusOf(record, version), deckCount, DisplayDate.Ago(record.UpdatedAt, now), record.Access, owner);

    // The palette's label, or "any" when the template brings none. A template's own palette is named by the template.
    private static string PaletteLabel(Template template, IPaletteCatalogue palettes)
    {
        if (template.Palette is null)
        {
            return ANY_PALETTE;
        }
        return template.Palette == "template" ? template.Label : palettes.Find(template.Palette)?.Label ?? template.Palette;
    }

    // A built-in has no versions and no owner, and is always published and open to everyone.
    public static TemplateRowView BuiltIn(ResolvedTemplate resolved, IPaletteCatalogue palettes, int deckCount, DateTimeOffset now)
    {
        var template = resolved.Template;
        return new TemplateRowView(
            resolved.Ref,
            template.Label,
            template.Name,
            template.Purpose,
            $"{template.Type.Head} / {template.Type.Body}",
            PaletteLabel(template, palettes),
            template.LockPalette,
            1,
            TemplateStatus.Published,
            deckCount,
            DisplayDate.Day(now),
            DeckSharing.Private,
            null);
    }

    public static TemplateStatus StatusOf(TemplateRecord record, int version)
    {
        if (record.ArchivedAt is not null)
        {
            return TemplateStatus.Archived;
        }
        return version > TemplateVersion.DRAFT ? TemplateStatus.Published : TemplateStatus.Draft;
    }
}
