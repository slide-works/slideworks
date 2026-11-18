using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates;

// The pure mapping from a stored template version to what ITemplates answers (T4). The manifest's file fields are asset
// ids already, so they pass through. Template.Name is the ref (the short name lives in the manifest), the ornament is
// "dots" and the title art and logo are left to the look, which carries them as assets.
public static class ManifestTemplates
{
    public static ResolvedTemplate Resolve(TemplateRecord record, TemplateVersion version, TemplateManifest manifest, int latestVersion, string? ownerName)
    {
        var template = new Template
        {
            Name = record.Id,
            Label = manifest.Label,
            Purpose = manifest.Purpose,
            IsDefault = false,
            Layouts = manifest.Layouts,
            Chrome = new TemplateChrome
            {
                Tracker = manifest.Chrome.Tracker,
                Footer = manifest.Chrome.Footer,
                Progress = manifest.Chrome.Progress,
                DeepDivesOnDivider = manifest.Chrome.DeepDivesOnDivider,
                Draft = manifest.Chrome.Draft,
            },
            Outline = manifest.Outline,
            HouseStyle = manifest.HouseStyle,
            Type = new TemplateType { Head = manifest.Type.Head, Body = manifest.Type.Body, HeadWeight = manifest.Type.HeadWeight, HeadTracking = manifest.Type.HeadTracking },
            Shape = new TemplateShape
            {
                Radius = new TemplateRadius { Sm = manifest.Shape.Radius.Sm, Md = manifest.Shape.Radius.Md, Lg = manifest.Shape.Radius.Lg, Xl = manifest.Shape.Radius.Xl },
                Shadow = manifest.Shape.Shadow,
            },
            TitleArt = null,
            Ornament = "dots",
            Logo = null,
            Palette = manifest.Palette is null ? null : ResolvedCatalogues.OWN_PALETTE,
            LockPalette = manifest.LockPalette,
        };

        return new ResolvedTemplate(template, record.Id, version.Version, false)
        {
            Look = LookOf(manifest),
            OwnPalette = manifest.Palette is null ? null : PaletteOf(manifest.Palette),
            LatestVersion = latestVersion,
            OwnerId = record.OwnerId,
            OwnerName = ownerName,
            Archived = record.ArchivedAt is not null,
            FitWidths = WidthsOf(version.FitWidthsJson),
            Layouts = name => template.Allows(name) ? LayoutCatalogue.Find(name) : null,
        };
    }

    private static TemplateLook LookOf(TemplateManifest manifest)
        => new(
            [.. manifest.Fonts.Select(f => new TemplateFont(f.Family, f.Role, f.File, f.Weight, f.Style))],
            manifest.Logos.Light,
            manifest.Logos.Dark,
            manifest.Logos.ShowOnDark,
            manifest.TitleArt.Kind,
            manifest.TitleArt.Generator,
            manifest.TitleArt.File,
            manifest.Ornament.Dots,
            manifest.Chrome.TopBar,
            [.. (manifest.Palette?.Extras ?? []).Select(e => new ExtraColour(e.Name, e.Colour, e.Ink))]);

    private static Palette PaletteOf(ManifestPalette palette)
    {
        var tokens = new Dictionary<string, string>(palette.Tokens, StringComparer.Ordinal);
        foreach (var extra in palette.Extras)
        {
            tokens[extra.Name] = extra.Colour;
            tokens[$"{extra.Name}-ink"] = extra.Ink;
        }
        return new Palette
        {
            Name = ResolvedCatalogues.OWN_PALETTE,
            Label = "Template",
            IsDefault = false,
            Words = [],
            Swatch = [.. new[] { "paper", "accent", "dd", "night" }.Where(tokens.ContainsKey).Select(k => tokens[k])],
            Tokens = tokens,
        };
    }

    // {"body": em, "head": em, "cell": em}; absent, or a stored value that does not read, means the default.
    private static FitWidths WidthsOf(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return FitWidths.Default;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new FitWidths(Read(root, "body"), Read(root, "head"), Read(root, "cell"));
        }
        catch (JsonException)
        {
            return FitWidths.Default;
        }
    }

    private static double Read(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetDouble(out var em) ? em : FitWidths.Default.Body;
}
