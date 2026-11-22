using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The markup stage is the security boundary for uploaded layouts: everything not on the allow-list is
// refused (never dropped), nothing of the upload reaches an error, and no input makes it throw.
public class MarkupSanitiserTests
{
    private static readonly MarkupSanitiser Sanitiser = new();

    private static string FixtureFolder()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "SlideWorks.UnitTest", "Look", "CustomLayouts", "Fixtures", "hostile-markup");
    }

    public static IEnumerable<TestCaseData> HostileFixtures()
    {
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(FixtureFolder(), "expected.json")))!;
        var files = Directory.GetFiles(FixtureFolder(), "*.html").Select(Path.GetFileName).Order(StringComparer.Ordinal);
        foreach (var file in files)
        {
            yield return new TestCaseData(file, expected[file!]).SetName($"Sanitise_WhenAHostileFixtureIsGiven_ShouldRefuse({file})");
        }
    }

    private static string SampleMarkup() =>
        JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-upload.sample.json")))!["markup"]!.GetValue<string>();

    [TestCaseSource(nameof(HostileFixtures))]
    public void Sanitise_WhenAHostileFixtureIsGiven_ShouldRefuse(string file, string code)
    {
        var (tree, errors) = Sanitiser.Sanitise(File.ReadAllText(Path.Combine(FixtureFolder(), file)));

        tree.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        errors[0].Code.ShouldBe(code, file);
    }

    [Test]
    public void Sanitise_WhenEveryExpectedFixtureIsListed_ShouldCoverTheRequiredHostileCases()
    {
        string[] required =
        [
            "script", "script-mixed-case", "onclick", "style-attribute", "style-element", "iframe", "form", "img-onerror",
            "svg-script", "link-import", "base-href", "entity-javascript", "entity-encoded-tag", "custom-entity",
            "billion-laughs", "comment", "cdata", "null-byte", "unclosed-tag", "text-in-markup", "class-uppercase",
            "var-sw-prefix", "each-three-deep", "step-number-on-each", "step-each-without-each", "slot-with-children",
            "duplicate-attribute", "xml-declaration", "processing-instruction", "namespaced-tag", "xmlns-attribute", "tag-uppercase",
        ];

        foreach (var name in required)
        {
            File.Exists(Path.Combine(FixtureFolder(), name + ".html")).ShouldBeTrue(name);
        }
    }

    [Test]
    public void Sanitise_WhenAScriptTagIsPresent_ShouldRefuse()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div><script></script></div>");

        tree.ShouldBeNull();
        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_TAG]);
    }

    [Test]
    public void Sanitise_WhenAnEventAttributeIsPresent_ShouldRefuse()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div onclick=\"x()\"></div>");

        tree.ShouldBeNull();
        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_ATTRIBUTE]);
    }

    [Test]
    public void Sanitise_WhenAnAttributeIsAnUpperCaseEventHandler_ShouldRefuse()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div ONCLICK=\"x()\"></div>");

        tree.ShouldBeNull();
        errors[0].Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_ATTRIBUTE);
    }

    [Test]
    public void Sanitise_WhenEachIsNested_ShouldBindTheInnerItem()
    {
        var (tree, errors) = Sanitiser.Sanitise(
            "<div data-each=\"groups\"><span data-slot=\"name\"></span><div data-each=\"items\"><span data-slot=\"label\"></span></div></div>");

        errors.ShouldBeEmpty();
        var outer = tree!.Root.Single();
        outer.Each.ShouldBe("groups");
        var inner = outer.Children.Single(child => child.Each is not null);
        inner.Each.ShouldBe("items");
        inner.Children.Single().Slot.ShouldBe("label");
    }

    [Test]
    public void Sanitise_WhenTheMarkupIsOversized_ShouldRefuseBeforeParsing()
    {
        var markup = new string('x', 5 * 1024 * 1024);
        var clock = Stopwatch.StartNew();

        var (tree, errors) = Sanitiser.Sanitise(markup);

        clock.ElapsedMilliseconds.ShouldBeLessThan(1000);
        tree.ShouldBeNull();
        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_TOO_LARGE]);
    }

    [Test]
    public void Sanitise_WhenMultiByteMarkupIsOverTheByteLimit_ShouldRefuse()
    {
        var markup = "<div class=\"a\"></div>" + new string('é', 140_000);

        Sanitiser.Sanitise(markup).Errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_TOO_LARGE]);
    }

    [Test]
    public void Sanitise_WhenNestingIsTooDeep_ShouldRefuse()
    {
        var markup = string.Concat(Enumerable.Repeat("<b>", 50_000));
        var clock = Stopwatch.StartNew();

        var (tree, errors) = Sanitiser.Sanitise(markup);

        clock.ElapsedMilliseconds.ShouldBeLessThan(1000);
        tree.ShouldBeNull();
        errors.Select(e => e.Code).ShouldContain(CustomLayoutErrorCodes.MARKUP_DEPTH);
    }

    [Test]
    public void Sanitise_WhenNestingIsExactlyAtTheLimit_ShouldPass()
    {
        var markup = string.Concat(Enumerable.Repeat("<div>", 12)) + string.Concat(Enumerable.Repeat("</div>", 12));

        Sanitiser.Sanitise(markup).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Sanitise_WhenThereAreTooManyNodes_ShouldRefuseAtTheFirstOneOver()
    {
        var markup = string.Concat(Enumerable.Repeat("<b></b>", 401));

        var (tree, errors) = Sanitiser.Sanitise(markup);

        tree.ShouldBeNull();
        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_TOO_MANY_NODES]);
        Sanitiser.Sanitise(string.Concat(Enumerable.Repeat("<b></b>", 400))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Sanitise_WhenAnElementHasTooManyClasses_ShouldRefuse()
    {
        Sanitiser.Sanitise("<div class=\"a b c d e f g h\"></div>").Errors.ShouldBeEmpty();
        Sanitiser.Sanitise("<div class=\"a b c d e f g h i\"></div>").Errors[0].Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_CLASS);
    }

    [Test]
    public void Sanitise_WhenAnUnknownElementOrAttributeIsGiven_ShouldRefuseBoth()
    {
        var (_, errors) = Sanitiser.Sanitise("<marquee data-x=\"1\"></marquee>");

        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.MARKUP_TAG, CustomLayoutErrorCodes.MARKUP_ATTRIBUTE]);
    }

    [Test]
    public void Sanitise_WhenABindingNamesNoKnownSlot_ShouldStillPassFormAndLeaveTheCrossCheckToLookIt()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div data-slot=\"nowhere\"></div>");

        errors.ShouldBeEmpty();
        tree!.Root.Single().Slot.ShouldBe("nowhere");
    }

    [Test]
    public void Sanitise_WhenAVariableIsBoundWithABadForm_ShouldRefuse()
    {
        foreach (var value in new[] { "--p", "p: progress", "--p: Progress", "--p: a: b", "--p: a;", "--sw-ink: a", "--p: a; --p: b", "" })
        {
            var (tree, errors) = Sanitiser.Sanitise($"<div data-var=\"{value}\"></div>");

            tree.ShouldBeNull(value);
            errors[0].Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_BINDING, value);
        }
    }

    [Test]
    public void Sanitise_WhenRefused_ShouldNotEchoTheInput()
    {
        foreach (var file in Directory.GetFiles(FixtureFolder(), "*.html"))
        {
            var markup = File.ReadAllText(file);
            var (_, errors) = Sanitiser.Sanitise(markup);

            foreach (var error in errors)
            {
                // The contract's own texts hold an apostrophe: only what follows the text is checked.
                var added = error.Message[CustomLayoutErrorCodes.Text(error.Code).Length..];
                error.Message.ShouldStartWith(CustomLayoutErrorCodes.Text(error.Code));
                added.ShouldMatch(@"^( \([a-z0-9_-]+\))?$");
                error.Message.ShouldNotContain("<");
                error.Message.ShouldNotContain("\"");
                error.Message.ShouldNotContain("alert");
                error.Message.ShouldNotContain("javascript");
                error.Path.ShouldStartWith("layout.html:");
            }
        }
    }

    [Test]
    public void Sanitise_WhenAnAttributeValueIsRefused_ShouldNotEchoTheValue()
    {
        var (_, errors) = Sanitiser.Sanitise("<div data-slot=\"SECRET_VALUE\" class=\"SECRET_CLASS\" onclick=\"SECRET_CODE\" SECRET_NAME=\"x\"></div><SECRET_TAG></SECRET_TAG>");

        errors.Count.ShouldBeGreaterThan(3);
        errors.ShouldAllBe(error => !error.Message.Contains("SECRET", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Sanitise_WhenARefusedNameIsALowerCaseIdentifier_ShouldNameItInTheMessage()
    {
        var (_, errors) = Sanitiser.Sanitise("<script></script>");

        errors.Single().Message.ShouldBe(CustomLayoutErrorCodes.Text(CustomLayoutErrorCodes.MARKUP_TAG) + " (script)");
    }

    [Test]
    public void Sanitise_WhenRefused_ShouldNameTheLine()
    {
        var (_, errors) = Sanitiser.Sanitise("<div>\n  <span data-slot=\"a\"></span>\n  <script></script>\n</div>");

        errors.Single().Path.ShouldBe("layout.html:3");
    }

    [Test]
    public void Sanitise_WhenTheMarkupIsMalformed_ShouldNameTheLineOfTheProblem()
    {
        var (_, errors) = Sanitiser.Sanitise("<div>\n<span>\n</div>");

        errors.Single().Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_MALFORMED);
        errors.Single().Path.ShouldBe("layout.html:3");
    }

    [Test]
    public void Sanitise_WhenTwoProblems_ShouldReturnBoth()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div>\n<script></script>\n<span onclick=\"x\"></span>\n</div>");

        tree.ShouldBeNull();
        errors.Select(e => (e.Path, e.Code)).ShouldBe(
        [
            ("layout.html:2", CustomLayoutErrorCodes.MARKUP_TAG),
            ("layout.html:3", CustomLayoutErrorCodes.MARKUP_ATTRIBUTE),
        ]);
    }

    [Test]
    public void Sanitise_WhenThereAreManyProblems_ShouldStopAtTwenty()
    {
        var (_, errors) = Sanitiser.Sanitise(string.Concat(Enumerable.Repeat("<script></script>", 100)));

        errors.Count.ShouldBe(20);
    }

    [Test]
    public void Sanitise_WhenTheSampleMarkupIsGiven_ShouldMatchTheSampleTree()
    {
        var (tree, errors) = Sanitiser.Sanitise(SampleMarkup());

        errors.ShouldBeEmpty();
        var expected = LayoutTreeJson.Read(File.ReadAllText(ContractFiles.PathOf("custom-layout-tree.sample.json")));
        tree.ShouldNotBeNull();
        tree.Scope.ShouldBeNull();
        JsonNode.DeepEquals(LayoutTreeJson.ToNode(tree), LayoutTreeJson.ToNode(expected)).ShouldBeTrue(LayoutTreeJson.Write(tree));
    }

    [Test]
    public void Sanitise_WhenAVarIsBound_ShouldCarryNameAndSlot()
    {
        var (tree, _) = Sanitiser.Sanitise("<div data-var=\"--p: progress; --q: other\"></div>");

        tree!.Root.Single().Vars.ShouldBe([new TreeVar("--p", "progress"), new TreeVar("--q", "other")]);
    }

    [Test]
    public void Sanitise_WhenStepIsEach_ShouldBeStepZero()
    {
        var (tree, _) = Sanitiser.Sanitise("<div data-each=\"rows\" data-step=\"each\"></div>");

        tree!.Root.Single().Step.ShouldBe(0);
    }

    [Test]
    public void Sanitise_WhenStepIsANumber_ShouldCarryIt()
    {
        var (tree, _) = Sanitiser.Sanitise("<div data-step=\"20\"></div><div data-step=\"1\"></div>");

        tree!.Root.Select(node => node.Step).ShouldBe([20, 1]);
    }

    [Test]
    public void Sanitise_WhenAnAttributeHasNoValue_ShouldRefuse()
    {
        var (tree, errors) = Sanitiser.Sanitise("<div data-slot></div>");

        tree.ShouldBeNull();
        errors.Single().Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_MALFORMED);
    }

    [Test]
    public void Sanitise_WhenIfIsPresent_ShouldCarryIt()
    {
        var (tree, _) = Sanitiser.Sanitise("<p data-if=\"note\" data-slot=\"note\"></p>");

        tree!.Root.Single().If.ShouldBe("note");
    }

    [Test]
    public void Sanitise_WhenVariantsAreBound_ShouldCarryBareSlotNames()
    {
        var (tree, _) = Sanitiser.Sanitise("<div data-variant=\"state kind\"></div>");

        tree!.Root.Single().Variants.ShouldBe([TreeVariant.Bare("state"), TreeVariant.Bare("kind")]);
    }

    [Test]
    public void Sanitise_WhenBAndIAreUsed_ShouldPass()
    {
        var (tree, errors) = Sanitiser.Sanitise("<p data-if=\"x\"><b></b><i></i><em></em><strong></strong></p>");

        errors.ShouldBeEmpty();
        tree!.Root.Single().Children.Select(child => child.Tag).ShouldBe(["b", "i", "em", "strong"]);
    }

    [Test]
    public void Sanitise_WhenATagIsRemovedFromTheAllowList_ShouldRefuseIt()
    {
        var allow = CustomLayoutAllow.Default with { Tags = CustomLayoutAllow.Default.Tags.Where(tag => tag != "span").ToList() };

        var (tree, errors) = new MarkupSanitiser(allow).Sanitise("<span></span>");

        tree.ShouldBeNull();
        errors.Single().Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_TAG);
        Sanitiser.Sanitise("<span></span>").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Sanitise_WhenAnAttributeIsRemovedFromTheAllowList_ShouldRefuseIt()
    {
        var allow = CustomLayoutAllow.Default with { Attributes = CustomLayoutAllow.Default.Attributes.Where(name => name != "data-if").ToList() };

        new MarkupSanitiser(allow).Sanitise("<div data-if=\"a\"></div>").Errors.Single().Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_ATTRIBUTE);
    }

    [Test]
    public void Sanitise_WhenGivenTheAgentLanesMarkup_ShouldPass()
    {
        const string markup = """
            <p class="kicker" data-slot="kicker"></p>
            <h2 class="headline" data-slot="headline"></h2>
            <div class="cols">
              <div class="main">
                <div class="lanes">
                  <div class="lane" data-each="lanes" data-variant="state" data-step="each">
                    <span class="who" data-slot="who"></span>
                    <div class="task">
                      <span class="task-name" data-slot="task"></span>
                      <div class="meter"><span class="meter-fill" data-var="--p: progress"></span></div>
                    </div>
                    <span class="status" data-slot="status"></span>
                  </div>
                </div>
                <p class="lead" data-slot="lead"></p>
              </div>
              <div class="ladder" data-slot="levels"></div>
            </div>
            """;

        var (tree, errors) = Sanitiser.Sanitise(markup);

        errors.ShouldBeEmpty();
        tree!.Root.Count.ShouldBe(3);
    }

    [Test]
    public void Sanitise_WhenGivenEmptyOrWhitespaceOnlyMarkup_ShouldRefuse()
    {
        foreach (var markup in new[] { string.Empty, "  \n\t " })
        {
            Sanitiser.Sanitise(markup).Errors.Single().Code.ShouldBe(CustomLayoutErrorCodes.MARKUP_MALFORMED);
        }
    }

    [Test]
    public void Sanitise_WhenGivenAnyOddInput_ShouldNeverThrow()
    {
        string[] odd = ["<", ">", "&", "&#;", "&#x110000;", "<div", "<div class=\"", "</div>", "\0", "￿", "<\u0000>", "<div/><div/>", "<!", "<![CDATA[", "&#xD800;"];

        foreach (var markup in odd)
        {
            var (tree, errors) = Sanitiser.Sanitise(markup);

            (tree is null).ShouldBe(errors.Count > 0, markup);
        }
    }

    [Test]
    public void Sanitise_WhenTheTreeIsReturned_ShouldHoldNoText()
    {
        var (tree, _) = Sanitiser.Sanitise("<div> \n <span data-slot=\"a\"></span> \n </div>");

        tree!.Nodes().Count().ShouldBe(2);
    }
}
