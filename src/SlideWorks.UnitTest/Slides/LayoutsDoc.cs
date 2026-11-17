using System.Globalization;
using System.Text;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// Renders src/contracts/layouts.md from the catalogue, so the doc can never drift from the code.
public static class LayoutsDoc
{
    public static string Render()
    {
        var doc = new StringBuilder();
        doc.Append("""
            # Layouts and inline blocks

            <!-- Generated from LayoutCatalogue (src/SlideWorks.Core/Slides). Do not edit by hand. To rewrite it:
                 UPDATE_LAYOUTS_DOC=1 dotnet test src/SlideWorks.UnitTest --filter "FullyQualifiedName~LayoutsDoc" -->

            A slide is a layout plus slots (K2). Slot text is plain text: Markdown and HTML are not interpreted,
            and line breaks are allowed only where a slot says so. Every slide also has `part` (1-based, or null)
            and `notes`, which are set next to the slots, not inside them. A JSON null counts as an absent slot.
            Error paths name the failing field with 0-based indexes, such as `practice.do[2].lead: required`.

            | Layout | Dark | Steps | Description |
            |---|---|---|---|

            """);
        foreach (var layout in LayoutCatalogue.Layouts)
        {
            var steps = layout.StepsFrom is null ? "none" : $"one per `{layout.StepsFrom}` item when `reveal` is `one-by-one`";
            doc.Append(CultureInfo.InvariantCulture, $"| [`{layout.Name}`](#{layout.Name}) | {(layout.Dark ? "yes" : "no")} | {steps} | {layout.Description} |\n");
        }

        foreach (var layout in LayoutCatalogue.Layouts)
        {
            doc.Append(CultureInfo.InvariantCulture, $"\n## {layout.Name}\n\n{layout.Description}\n\n");
            doc.Append("| Slot | Kind | Required | Limits | Notes |\n|---|---|---|---|---|\n");
            foreach (var field in layout.Slots.Fields)
            {
                AppendField(doc, "", field.Name, field.Type, field.Required, field.Description);
            }
        }

        doc.Append("\n## Inline blocks\n\n| Block | Kind | Required | Limits | Description |\n|---|---|---|---|---|\n");
        foreach (var block in LayoutCatalogue.Blocks)
        {
            var (kind, limits) = block.Type is null ? ("rendered, never set by agents", "") : Describe(block.Type);
            doc.Append(CultureInfo.InvariantCulture, $"| `{block.Name}` | {kind} | — | {limits} | {block.Description} |\n");
            if (block.Type is not null)
            {
                AppendChildren(doc, block.Name, block.Type);
            }
        }

        return doc.ToString();
    }

    private static void AppendField(StringBuilder doc, string prefix, string name, ISlotType type, bool required, string? description)
    {
        var path = prefix + name;
        var (kind, limits) = Describe(type);
        doc.Append(CultureInfo.InvariantCulture, $"| `{path}` | {kind} | {(required ? "yes" : "no")} | {limits} | {description} |\n");
        AppendChildren(doc, path, type);
    }

    private static void AppendChildren(StringBuilder doc, string path, ISlotType type)
    {
        switch (type)
        {
            case ObjectSlot obj:
                AppendFields(doc, path + ".", obj.Fields);
                break;
            case ImageSlot image:
                AppendFields(doc, path + ".", image.Fields);
                break;
            case TextOrObjectSlot either:
                AppendFields(doc, path + ".", either.Richer.Fields);
                break;
            case ListSlot list:
                AppendChildren(doc, path + "[]", list.Item);
                break;
            case BlockSlot { Block.Type: { } blockType }:
                AppendChildren(doc, path, blockType);
                break;
            default:
                break;
        }
    }

    private static void AppendFields(StringBuilder doc, string prefix, IReadOnlyList<SlotField> fields)
    {
        foreach (var field in fields)
        {
            AppendField(doc, prefix, field.Name, field.Type, field.Required, field.Description);
        }
    }

    private static (string Kind, string Limits) Describe(ISlotType type) => type switch
    {
        TextSlot text => ("text", $"up to {text.MaxLength} chars" + (text.LineBreaks ? ", line breaks allowed" : "")),
        NumberSlot number => (number.Kind, $"{Number(number.Minimum)} to {Number(number.Maximum)}"),
        EnumSlot choice => (string.Join(" or ", choice.Values.Select(v => $"`{v}`")), EnumLimits(choice)),
        ObjectSlot obj => ("object", obj.ExactlyOneOf.Count > 0 ? "exactly one of " + string.Join(", ", obj.ExactlyOneOf.Select(f => $"`{f}`")) : ""),
        ImageSlot => ("image `{image, alt}`", ""),
        TextOrObjectSlot either => ("text or object", $"text up to {either.Text.MaxLength} chars"),
        BlockSlot block => ($"block `{block.Block.Name}`", block.Block.Type is null ? "" : Describe(block.Block.Type).Limits),
        ListSlot list => ($"list of {Describe(list.Item).Kind}", Join($"{list.MinItems}–{list.MaxItems} items", Describe(list.Item).Limits)),
        _ => (type.Kind, ""),
    };

    private static string EnumLimits(EnumSlot choice)
    {
        var parts = new List<string>();
        if (choice.Default is not null)
        {
            parts.Add($"default `{choice.Default}`");
        }

        if (choice.Aliases.Count > 0)
        {
            parts.Add("aliases " + string.Join(", ", choice.Aliases.Select(a => $"`{a.Key}` = `{a.Value}`")));
        }

        return string.Join("; ", parts);
    }

    private static string Join(string count, string itemLimits) => itemLimits.Length == 0 ? count : $"{count}; each {itemLimits}";

    private static string Number(double value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);
}
