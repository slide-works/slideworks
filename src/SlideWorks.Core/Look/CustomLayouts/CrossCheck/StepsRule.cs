namespace SlideWorks.Core.Look.CustomLayouts.CrossCheck;

// The steps rule (T10): none, list or fixed, never both. Null steps means steps_mismatch at layout.
public static class StepsRule
{
    private sealed record Frame(TreeNode Node, bool InEach);

    public static CustomSteps? Of(LayoutTree tree, CustomLayoutSchema schema)
    {
        var listElements = 0;
        var numbers = new HashSet<int>();
        var broken = false;

        var stack = new Stack<Frame>();
        foreach (var root in Enumerable.Reverse(tree.Root))
        {
            stack.Push(new Frame(root, false));
        }

        while (stack.Count > 0)
        {
            var (node, inEach) = stack.Pop();
            if (node.Step is { } step)
            {
                if (step == TreeNode.STEP_EACH)
                {
                    // One step per item: on an outermost each that is the schema's list, nowhere else.
                    if (node.Each is not null && !inEach && schema.Steps is not null && node.Each == schema.Steps)
                    {
                        listElements++;
                    }
                    else
                    {
                        broken = true;
                    }
                }
                else if (step is < 1 or > CustomLayoutLimits.MAX_STEPS || node.Each is not null || inEach)
                {
                    broken = true;
                }
                else
                {
                    numbers.Add(step);
                }
            }

            foreach (var child in node.Children)
            {
                stack.Push(new Frame(child, inEach || node.Each is not null));
            }
        }

        if (broken)
        {
            return null;
        }

        if (schema.Steps is not null)
        {
            return listElements == 1 && numbers.Count == 0 ? new CustomSteps(CustomSteps.LIST, schema.Steps, 0) : null;
        }

        if (listElements > 0)
        {
            return null;
        }

        if (numbers.Count == 0)
        {
            return new CustomSteps(CustomSteps.NONE, null, 0);
        }

        return numbers.Max() == numbers.Count ? new CustomSteps(CustomSteps.FIXED, null, numbers.Count) : null;
    }
}
