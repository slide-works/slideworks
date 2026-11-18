using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Drafts;

// The first manifest of a draft that starts from a built-in template: its structure, type, shape, title art and
// ornament, with the label a person typed, no fonts, no logos, no palette and no custom layouts.
public static class DraftManifests
{
    public static TemplateManifest StartingFrom(ResolvedTemplate builtIn, string label)
    {
        var template = builtIn.Template;
        var look = builtIn.Look;
        return new TemplateManifest
        {
            Name = ShortNames.Of(label),
            Label = label,
            Purpose = template.Purpose,
            BasedOn = builtIn.Ref,
            Layouts = [.. template.Layouts],
            Chrome = new ManifestChrome
            {
                Tracker = template.Chrome.Tracker,
                Footer = template.Chrome.Footer,
                Progress = template.Chrome.Progress,
                DeepDivesOnDivider = template.Chrome.DeepDivesOnDivider,
                Draft = template.Chrome.Draft,
                TopBar = look.TopBar,
            },
            Outline = [.. template.Outline],
            HouseStyle = [.. template.HouseStyle],
            Type = new ManifestType
            {
                Head = template.Type.Head,
                Body = template.Type.Body,
                HeadWeight = template.Type.HeadWeight,
                HeadTracking = template.Type.HeadTracking,
            },
            Shape = new ManifestShape
            {
                Radius = new ManifestRadius
                {
                    Sm = template.Shape.Radius.Sm,
                    Md = template.Shape.Radius.Md,
                    Lg = template.Shape.Radius.Lg,
                    Xl = template.Shape.Radius.Xl,
                },
                Shadow = template.Shape.Shadow,
            },
            Fonts = [],
            Logos = new ManifestLogos { ShowOnDark = false },
            TitleArt = new ManifestTitleArt { Kind = look.ArtKind, Generator = look.ArtGenerator },
            Ornament = new ManifestOrnament { Dots = [.. look.OrnamentColours] },
            LockPalette = false,
            CustomLayouts = [],
        };
    }
}
