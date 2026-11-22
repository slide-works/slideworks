using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

public class LayoutCrossCheckTests
{
    private static readonly LayoutCrossCheck Subject = new();

    [Test]
    public void Check_WhenGivenTheSampleTreeAndSchema_ShouldReturnListSteps()
    {
        var tree = LayoutTreeJson.Read(File.ReadAllText(ContractFiles.PathOf("custom-layout-tree.sample.json")));
        var bars = List("bars", 2, 5, Num("share"), Text("label"), Enum("state", "normal", "alert"));

        var (steps, errors) = Subject.Check(tree, Schema("bars", Text("headline"), bars, Text("note") with { Optional = true }));

        errors.ShouldBeEmpty();
        steps.ShouldBe(new CustomSteps(CustomSteps.LIST, "bars", 0));
    }

    [Test]
    public void Check_WhenAVarNamesATextSlot_ShouldRefuse()
    {
        Refuses(Tree(N(vars: [new TreeVar("--v", "title")])), Schema(null, Text("title")), CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.title").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenAVariantNamesANumber_ShouldRefuse()
    {
        Refuses(Tree(N(variants: [TreeVariant.Bare("size")])), Schema(null, Num("size")), CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.size").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenEachNamesAText_ShouldRefuse()
    {
        Refuses(Tree(N(each: "title")), Schema(null, Text("title")), CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.title").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenIfNamesARequiredText_ShouldRefuse()
    {
        Refuses(Tree(N(slot: "title", iff: "title")), Schema(null, Text("title")), CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.title").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenSlotNamesAList_ShouldRefuse()
    {
        Refuses(Tree(N(slot: "rows"), N(each: "rows")), Schema(null, List("rows", 1, 3, Text("x"))), CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.rows").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenABlockSitsOnASpan_ShouldRefuse()
    {
        var schema = Schema(null, new CustomSlot("badge", "badge", false, null, null, [], [], null));

        Refuses(Tree(N(tag: "span", slot: "badge")), schema, CustomLayoutErrorCodes.BINDING_WRONG_KIND, "slots.badge").ShouldNotBeEmpty();
        Subject.Check(Tree(N(slot: "badge")), schema).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenANameIsNotInTheSchema_ShouldRefuseWithoutEchoingIt()
    {
        var (_, errors) = Subject.Check(Tree(N(slot: "secret_name"), N(slot: "title")), Schema(null, Text("title")));

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT);
        error.Path.ShouldBe("layout");
        error.Message.ShouldNotContain("secret_name");
    }

    [Test]
    public void Check_WhenAnInnerNameReadsTheOuterItem_ShouldRefuse()
    {
        Refuses(
            Tree(N(slot: "title"), N(each: "rows", children: [N(slot: "title")])),
            Schema(null, Text("title"), List("rows", 1, 3, Text("x"))),
            CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT,
            "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenItemIsUsedInAListOfObjects_ShouldRefuse()
    {
        Refuses(Tree(N(each: "rows", children: [N(slot: "item")])), Schema(null, List("rows", 1, 3, Text("x"))), CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenItemIsUsedInAListOfScalars_ShouldPass()
    {
        var tags = new CustomSlot("tags", "list", false, 1, 4, [], [], Text("item"));

        Subject.Check(Tree(N(each: "tags", children: [N(slot: "item")])), Schema(null, tags)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenAnInnerEachNamesAFieldList_ShouldPass()
    {
        var groups = List("groups", 1, 3, Text("name"), List("people", 1, 4, Text("who")));
        var tree = Tree(N(each: "groups", children: [N(slot: "name"), N(each: "people", children: [N(slot: "who")])]));

        Subject.Check(tree, Schema(null, groups)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenASlotIsNeverBound_ShouldRefuse()
    {
        Refuses(Tree(N(slot: "title")), Schema(null, Text("title"), Text("spare")), CustomLayoutErrorCodes.SLOT_UNUSED, "slots.spare").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenAListIsOnlyUsedByEach_ShouldPass()
    {
        Subject.Check(Tree(N(each: "rows")), Schema(null, List("rows", 1, 3, Text("x")))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenTheSteppedListIsNested_ShouldPass()
    {
        var tree = Tree(N(children: [N(children: [N(children: [N(children: [N(each: "lanes", step: 0)])])])]));

        var (steps, errors) = Subject.Check(tree, Schema("lanes", List("lanes", 1, 4, Text("x"))));

        errors.ShouldBeEmpty();
        steps.ShouldBe(new CustomSteps(CustomSteps.LIST, "lanes", 0));
    }

    [Test]
    public void Check_WhenStepsDisagreeWithTheMarkup_ShouldRefuse()
    {
        var rows = List("rows", 1, 3, Text("x"));
        var other = List("other", 1, 3, Text("y"));

        // Steps set, no step 0.
        Refuses(Tree(N(each: "rows"), N(each: "other")), Schema("rows", rows, other), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
        // Step 0, no Steps.
        Refuses(Tree(N(each: "rows", step: 0)), Schema(null, rows), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
        // Two elements with step 0.
        Refuses(Tree(N(each: "rows", step: 0), N(each: "rows", step: 0)), Schema("rows", rows), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
        // Steps names another list.
        Refuses(Tree(N(each: "rows", step: 0), N(each: "other")), Schema("other", rows, other), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenFixedStepsHaveAGap_ShouldRefuse()
    {
        Refuses(Tree(N(step: 1), N(step: 3)), Schema(null), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenFixedStepsAreOneToThree_ShouldCountThree()
    {
        var (steps, errors) = Subject.Check(Tree(N(step: 1), N(step: 2, children: [N(step: 2)]), N(step: 3)), Schema(null));

        errors.ShouldBeEmpty();
        steps.ShouldBe(new CustomSteps(CustomSteps.FIXED, null, 3));
    }

    [Test]
    public void Check_WhenANumberedStepIsInsideEach_ShouldRefuse()
    {
        var schema = Schema(null, List("rows", 1, 3, Text("x")));

        Refuses(Tree(N(each: "rows", children: [N(step: 1)])), schema, CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
        Refuses(Tree(N(each: "rows", step: 1)), schema, CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenListAndFixedStepsAreMixed_ShouldRefuse()
    {
        Refuses(Tree(N(each: "rows", step: 0), N(step: 1)), Schema("rows", List("rows", 1, 3, Text("x"))), CustomLayoutErrorCodes.STEPS_MISMATCH, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenThereAreNoSteps_ShouldReturnNone()
    {
        var (steps, errors) = Subject.Check(Tree(N()), Schema(null));

        errors.ShouldBeEmpty();
        steps.ShouldBe(new CustomSteps(CustomSteps.NONE, null, 0));
    }

    [Test]
    public void Check_WhenRepeatsMultiplyPastTheLimit_ShouldRefuse()
    {
        var inner = List("inner", 1, 12, Text("x"));
        var outer = List("outer", 1, 12, inner);
        var item = Enumerable.Range(0, 19).Select(_ => N()).ToArray();
        var tree = Tree(N(each: "outer", children: [N(each: "inner", children: item)]));

        Refuses(tree, Schema(null, outer), CustomLayoutErrorCodes.MARKUP_TOO_MANY_NODES, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenRepeatsStayUnderTheLimit_ShouldPass()
    {
        var item = Enumerable.Range(0, 99).Select(_ => N()).ToArray();
        var tree = Tree(N(each: "rows", children: item));

        Subject.Check(tree, Schema(null, List("rows", 1, 12, Text("x")))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenAListHasNoMaximum_ShouldRefuseWithoutOverflowing()
    {
        var tree = Tree(N(each: "a", children: [N(each: "b", children: [N(each: "c")])]));
        var c = List("c", 1, null, Text("x"));
        var b = List("b", 1, null, c);
        var a = List("a", 1, null, b);

        Refuses(tree, Schema(null, a), CustomLayoutErrorCodes.MARKUP_TOO_MANY_NODES, "layout").ShouldNotBeEmpty();
    }

    [Test]
    public void Check_WhenTwoProblems_ShouldReturnBoth()
    {
        var (steps, errors) = Subject.Check(Tree(N(slot: "nope")), Schema(null, Text("spare")));

        steps.ShouldBeNull();
        errors.Select(error => error.Code).ShouldBe([CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT, CustomLayoutErrorCodes.SLOT_UNUSED], ignoreOrder: true);
    }

    [Test]
    public void Check_WhenTheTreeIsVeryDeep_ShouldNotOverflowTheStack()
    {
        var node = N();
        for (var i = 0; i < 50_000; i++)
        {
            node = N(children: [node]);
        }

        Subject.Check(Tree(node), Schema(null)).Errors.Count.ShouldBe(1);
    }

    [Test]
    public void Check_WhenManyProblems_ShouldReturnAtMostTwenty()
    {
        var slots = Enumerable.Range(0, 30).Select(i => Text($"s{i}")).ToArray();

        Subject.Check(Tree(N()), Schema(null, slots)).Errors.Count.ShouldBe(20);
    }

    private static IReadOnlyList<CustomLayoutError> Refuses(LayoutTree tree, CustomLayoutSchema schema, string code, string path)
    {
        var (steps, errors) = Subject.Check(tree, schema);

        steps.ShouldBeNull();
        errors.ShouldContain(error => error.Code == code && error.Path == path);
        return errors;
    }

    private static LayoutTree Tree(params TreeNode[] root) => new(1, null, root);

    private static TreeNode N(
        string tag = "div",
        string? slot = null,
        string? each = null,
        string? iff = null,
        int? step = null,
        TreeVar[]? vars = null,
        TreeVariant[]? variants = null,
        TreeNode[]? children = null) =>
        new(tag, [], slot, each, iff, step, vars ?? [], variants ?? [], children ?? []);

    private static CustomLayoutSchema Schema(string? steps, params CustomSlot[] slots) => new("test-layout", "A test layout.", "light", steps, slots);

    private static CustomSlot Text(string name) => new(name, "text", false, null, 40, [], [], null);

    private static CustomSlot Num(string name) => new(name, "number", false, 0, 100, [], [], null);

    private static CustomSlot Enum(string name, params string[] values) => new(name, "enum", false, null, null, values, [], null);

    private static CustomSlot List(string name, int min, int? max, params CustomSlot[] fields) => new(name, "list", false, min, max, [], fields, null);
}
