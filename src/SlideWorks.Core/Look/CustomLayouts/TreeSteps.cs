namespace SlideWorks.Core.Look.CustomLayouts;

public static class TreeSteps
{
    // The highest numbered step in the tree, 0 when there is none. A step of 0 (data-step="each")
    // is not a number. Whether the numbers are legal is the cross-check's rule.
    public static int FixedCount(LayoutTree tree) =>
        tree.Nodes().Select(node => node.Step ?? TreeNode.STEP_EACH).DefaultIfEmpty(TreeNode.STEP_EACH).Max();
}
