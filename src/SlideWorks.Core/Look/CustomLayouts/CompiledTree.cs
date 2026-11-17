namespace SlideWorks.Core.Look.CustomLayouts;

// The compiled side of a tree: the scope set, every class written with the layout's prefix and every
// variant carrying the finished class of each value of its enum. Of makes it, in the pipeline; Verify
// checks one that was stored. A slot name on an element is looked up where the element stands: at the
// top level, or among the fields of the innermost data-each around it (the element that carries the
// data-each included), never further out.
public static class CompiledTree
{
    // Run after the cross-check passed, so a variant that names no enum here is our bug.
    public static LayoutTree Of(LayoutTree tree, CustomLayoutSchema schema, string scope, LayoutClassPrefix prefix) =>
        tree with { Scope = scope, Root = [.. tree.Root.Select(node => Scoped(node, schema, prefix))] };

    // Fails closed on a stored tree: every class, a variant's classes included, must be this layout's
    // own, and a variant's map must hold exactly the values of its enum.
    public static void Verify(LayoutTree tree, CustomLayoutSchema schema, LayoutClassPrefix prefix)
    {
        foreach (var node in tree.Root)
        {
            Verify(node, schema, prefix);
        }
    }

    private static TreeNode Scoped(TreeNode node, CustomLayoutSchema outer, LayoutClassPrefix prefix)
    {
        var slots = Around(node, outer) ?? throw new InvalidOperationException("The cross-check passed a repeated element that names no list.");
        return node with
        {
            Classes = [.. node.Classes.Select(prefix.Apply)],
            Variants = [.. node.Variants.Select(variant => WithClasses(variant, slots, prefix))],
            Children = [.. node.Children.Select(child => Scoped(child, slots, prefix))],
        };
    }

    private static TreeVariant WithClasses(TreeVariant variant, CustomLayoutSchema slots, LayoutClassPrefix prefix)
    {
        var choice = EnumAt(variant.Slot, slots) ?? throw new InvalidOperationException("The cross-check passed a variant that names no enum slot.");
        return variant with { Classes = [.. choice.Values.Select(value => new VariantClass(value, prefix.Apply(TreeVariant.CLASS_START + value)))] };
    }

    private static void Verify(TreeNode node, CustomLayoutSchema outer, LayoutClassPrefix prefix)
    {
        var layout = outer.Name;
        var slots = Around(node, outer) ?? throw new StoredLayoutRefusedException(layout, "a repeated element names no list of the layout's schema");
        if (!node.Classes.All(prefix.Owns))
        {
            throw new StoredLayoutRefusedException(layout, "a class in its tree does not start with the layout's own prefix");
        }

        foreach (var variant in node.Variants)
        {
            var choice = EnumAt(variant.Slot, slots) ?? throw new StoredLayoutRefusedException(layout, "a variant names no enum slot of the layout's schema");
            var classes = variant.Classes ?? [];
            if (!classes.Select(entry => entry.Value).Order(StringComparer.Ordinal).SequenceEqual(choice.Values.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new StoredLayoutRefusedException(layout, "a variant's classes are not one for each value of its enum");
            }

            if (!classes.All(entry => prefix.Owns(entry.Class)))
            {
                throw new StoredLayoutRefusedException(layout, "a variant's class does not start with the layout's own prefix");
            }
        }

        foreach (var child in node.Children)
        {
            Verify(child, slots, prefix);
        }
    }

    // The slots an element's own bindings and its children read: the fields of the list it repeats,
    // or the ones around it. Null when its data-each names no list there.
    private static CustomLayoutSchema? Around(TreeNode node, CustomLayoutSchema outer)
    {
        if (node.Each is null)
        {
            return outer;
        }

        var list = outer.Slots.FirstOrDefault(slot => slot.Name == node.Each && slot.Type == CustomSlot.LIST);
        if (list is null)
        {
            return null;
        }

        return outer with { Slots = list.Item is null ? list.Fields : [list.Item] };
    }

    private static CustomSlot? EnumAt(string name, CustomLayoutSchema around) =>
        around.Slots.FirstOrDefault(slot => slot.Name == name && slot.Type == CustomSlot.ENUM);
}
