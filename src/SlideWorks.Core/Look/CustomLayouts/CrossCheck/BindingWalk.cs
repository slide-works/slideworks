namespace SlideWorks.Core.Look.CustomLayouts.CrossCheck;

// The first rules of the cross-check, in one pass over the tree: every name resolves where its element
// stands (CompiledTree's lookup: the top level, or the fields of the innermost data-each, element
// included, never further out), has the kind its binding needs, every top-level slot is used, and the
// drawn nodes stay under the limit. An explicit stack, so the tree's depth cannot overflow ours.
public static class BindingWalk
{
    private const string SLOTS = "slots";

    // The scalar-or-block types that stand on a div only.
    private static readonly HashSet<string> DivOnly = ["image", "callout", "chip", "badge", "levels", "facts"];

    private sealed record Frame(TreeNode Node, IReadOnlyList<CustomSlot> Scope, string Path, bool InEach, long Repeats);

    public static IReadOnlyList<CustomLayoutError> Run(LayoutTree tree, CustomLayoutSchema schema)
    {
        var errors = new List<CustomLayoutError>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        long drawn = 0;

        var stack = new Stack<Frame>();
        foreach (var root in Enumerable.Reverse(tree.Root))
        {
            stack.Push(new Frame(root, schema.Slots, SLOTS, false, 1));
        }

        while (stack.Count > 0)
        {
            var (node, scope, path, inEach, repeats) = stack.Pop();
            var topLevel = !inEach;
            var here = scope;
            var herePath = path;

            if (node.Each is not null)
            {
                var list = Find(scope, node.Each);
                if (topLevel && list is not null)
                {
                    used.Add(list.Name);
                }

                if (list is null)
                {
                    errors.Add(Error(path, CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT, null));
                    here = [];
                }
                else if (list.Type != CustomSlot.LIST)
                {
                    errors.Add(Error($"{path}.{list.Name}", CustomLayoutErrorCodes.BINDING_WRONG_KIND, list.Name));
                    here = [];
                }
                else
                {
                    here = list.Item is null ? list.Fields : [list.Item with { Name = CustomSlot.ITEM }];
                    herePath = $"{path}.{list.Name}";
                    repeats = Math.Min(repeats * (list.Max ?? CustomLayoutLimits.MAX_DRAWN_NODES + 1L), CustomLayoutLimits.MAX_DRAWN_NODES + 1L);
                }
            }

            drawn += repeats;
            var bindsTop = topLevel && node.Each is null;
            if (node.Slot is { } slot && Resolve(here, slot, errors) is { } bound)
            {
                Note(used, bindsTop, bound);
                if (bound.Type == CustomSlot.LIST || (DivOnly.Contains(bound.Type) && node.Tag != "div"))
                {
                    errors.Add(Error($"{herePath}.{bound.Name}", CustomLayoutErrorCodes.BINDING_WRONG_KIND, bound.Name));
                }
            }

            if (node.If is { } condition && Resolve(here, condition, errors) is { } test)
            {
                Note(used, bindsTop, test);
                if (!test.Optional && test.Type != CustomSlot.LIST)
                {
                    errors.Add(Error($"{herePath}.{test.Name}", CustomLayoutErrorCodes.BINDING_WRONG_KIND, test.Name));
                }
            }

            foreach (var variable in node.Vars)
            {
                Require(here, herePath, variable.Slot, "number", used, bindsTop, errors);
            }

            foreach (var variant in node.Variants)
            {
                Require(here, herePath, variant.Slot, CustomSlot.ENUM, used, bindsTop, errors);
            }

            // An each element's own repeat counts for itself too: it is drawn once per item.
            foreach (var child in Enumerable.Reverse(node.Children))
            {
                stack.Push(new Frame(child, here, herePath, inEach || node.Each is not null, repeats));
            }
        }

        foreach (var name in schema.Slots.Select(slot => slot.Name).Where(name => !used.Contains(name)))
        {
            errors.Add(Error($"{SLOTS}.{name}", CustomLayoutErrorCodes.SLOT_UNUSED, name));
        }

        if (drawn > CustomLayoutLimits.MAX_DRAWN_NODES)
        {
            errors.Add(CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.MARKUP_TOO_MANY_NODES));
        }

        return errors;
    }

    private static CustomSlot? Find(IReadOnlyList<CustomSlot> scope, string name) =>
        scope.FirstOrDefault(slot => string.Equals(slot.Name, name, StringComparison.Ordinal));

    private static CustomSlot? Resolve(IReadOnlyList<CustomSlot> scope, string name, List<CustomLayoutError> errors)
    {
        var found = Find(scope, name);
        if (found is null)
        {
            errors.Add(Error(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT, null));
        }

        return found;
    }

    private static void Require(IReadOnlyList<CustomSlot> scope, string path, string name, string type, HashSet<string> used, bool top, List<CustomLayoutError> errors)
    {
        if (Resolve(scope, name, errors) is { } found)
        {
            Note(used, top, found);
            if (found.Type != type)
            {
                errors.Add(Error($"{path}.{found.Name}", CustomLayoutErrorCodes.BINDING_WRONG_KIND, found.Name));
            }
        }
    }

    private static void Note(HashSet<string> used, bool top, CustomSlot slot)
    {
        if (top)
        {
            used.Add(slot.Name);
        }
    }

    // Only a name the schema holds may reach a message or a path; anything else is left out.
    private static CustomLayoutError Error(string path, string code, string? name)
    {
        try
        {
            return name is null ? CustomLayoutError.Of(path, code) : CustomLayoutError.Of(path, code, name);
        }
        catch (ArgumentException)
        {
            return CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, code);
        }
    }
}
