using System.Text.Json;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The CSS sanitiser is a security boundary: the CSS that reaches a page is text it writes from checked
// rules. Every hostile form has its own fixture and a named code.
public class CssSanitiserTests
{
    private const string PREFIX = "cl-0a1b2c3d-bar-chart__";
    private const string SCOPE = ".cl-0a1b2c3d-bar-chart__";

    private static readonly string[] Bound = ["--v", "--p", "--w"];

    private readonly CssSanitiser sanitiser = new();

    private static string FixtureFolder => Path.Combine(
        Path.GetDirectoryName(Path.GetDirectoryName(ContractFiles.PathOf("custom-layout-allow.json")))!, "SlideWorks.UnitTest", "Look", "CustomLayouts", "Fixtures", "hostile-css");

    public static IEnumerable<TestCaseData> HostileFixtures()
    {
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(FixtureFolder, "expected.json")))!;
        foreach (var (file, code) in expected.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            yield return new TestCaseData(file, code).SetArgDisplayNames(file);
        }
    }

    private (string? Css, IReadOnlyList<CustomLayoutError> Errors) Run(string css) => sanitiser.Sanitise(css, PREFIX, Bound);

    private string Pass(string css)
    {
        var (written, errors) = Run(css);
        errors.ShouldBeEmpty();
        return written.ShouldNotBeNull();
    }

    private string FirstRefusal(string css)
    {
        var (written, errors) = Run(css);
        written.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        return errors[0].Code;
    }

    [TestCaseSource(nameof(HostileFixtures))]
    public void Sanitise_WhenAHostileFixtureIsGiven_ShouldRefuse(string file, string code)
    {
        var css = File.ReadAllText(Path.Combine(FixtureFolder, file));

        FirstRefusal(css).ShouldBe(code);
    }

    [Test]
    public void Sanitise_WhenTheHostileFixturesAreListed_ShouldCoverEveryFile()
    {
        var files = Directory.GetFiles(FixtureFolder, "*.css").Select(Path.GetFileName).Order(StringComparer.Ordinal);
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(FixtureFolder, "expected.json")))!;

        expected.Keys.Order(StringComparer.Ordinal).ShouldBe(files);
        foreach (var code in expected.Values)
        {
            CustomLayoutErrorCodes.All.ShouldContain(code);
        }
    }

    [Test]
    public void Sanitise_WhenUrlIsUsed_ShouldRefuse()
    {
        FirstRefusal(".a { background: url(x.png); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { background: var(--sw-ink) URL(x.png); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { mask: linear-gradient(var(--sw-ink), url(x.png)); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [TestCase("#fff")]
    [TestCase("#0A0a0a80")]
    [TestCase("rgb(1, 2, 3)")]
    [TestCase("hsl(1, 2%, 3%)")]
    [TestCase("red")]
    [TestCase("RebeccaPurple")]
    [TestCase("black")]
    [TestCase("Canvas")]
    public void Sanitise_WhenAColourIsALiteral_ShouldRefuse(string colour)
    {
        var (css, errors) = Run($".a {{ color: {colour}; }}");

        css.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        errors.Select(e => e.Code).ShouldAllBe(c => c == CustomLayoutErrorCodes.CSS_COLOUR || c == CustomLayoutErrorCodes.CSS_VALUE || c == CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [Test]
    public void Sanitise_WhenCalcReadsABoundVariable_ShouldPass()
    {
        var css = Pass(".a { width: calc(var(--p) * 1%); flex-grow: var(--w); transform: rotate(calc((var(--v) - 50) * 1.8deg)); }");

        css.ShouldContain("width: calc(var(--p) * 1%);");
        css.ShouldContain("flex-grow: var(--w);");
        css.ShouldContain("transform: rotate(calc((var(--v) - 50) * 1.8deg));");
    }

    [TestCase(".a { font-family: Inter; }")]
    [TestCase(".a { font-family: var(--sw-ink); }")]
    [TestCase(".a { font-family: var(--sw-font-body), var(--sw-font-head); }")]
    [TestCase(".a { font-family: var(--sw-font-body) var(--sw-font-head); }")]
    public void Sanitise_WhenAFontIsNamed_ShouldRefuse(string css)
    {
        FirstRefusal(css).ShouldBe(CustomLayoutErrorCodes.CSS_FONT);
    }

    [Test]
    public void Sanitise_WhenAFontIsQuotedOrShorthand_ShouldRefuse()
    {
        FirstRefusal(".a { font-family: \"Inter\"; }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
        FirstRefusal(".a { font: 12px Arial; }").ShouldBe(CustomLayoutErrorCodes.CSS_PROPERTY);
    }

    [Test]
    public void Sanitise_WhenCommentsHideAKeyword_ShouldRefuseAsAComment()
    {
        FirstRefusal(".a { background: u/**/rl(x); }").ShouldBe(CustomLayoutErrorCodes.CSS_COMMENT);
        FirstRefusal(".a { width: 1px; } /* @import */").ShouldBe(CustomLayoutErrorCodes.CSS_COMMENT);
    }

    [Test]
    public void Sanitise_WhenAnEscapeSpellsAKeyword_ShouldRefuseAsMalformed()
    {
        FirstRefusal(".a { background: \\75rl(x); }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
        FirstRefusal(".\\61 { width: 1px; }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
    }

    [Test]
    public void Sanitise_WhenImportantIsHiddenByWhitespaceOrCase_ShouldRefuseAsImportant()
    {
        FirstRefusal(".a { width: 1px ! IMPORTANT; }").ShouldBe(CustomLayoutErrorCodes.CSS_IMPORTANT);
        FirstRefusal(".a { width: 1px !important }").ShouldBe(CustomLayoutErrorCodes.CSS_IMPORTANT);
        FirstRefusal(".a { width: 1px !; }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
    }

    [Test]
    public void Sanitise_WhenAControlOrNonAsciiCharacterIsUsed_ShouldRefuse()
    {
        FirstRefusal(".a { width: 1px;\u0000 }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
        FirstRefusal(".a { width: 1px; }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
        FirstRefusal(".a { width: 1๑px; }").ShouldBe(CustomLayoutErrorCodes.CSS_MALFORMED);
    }

    [Test]
    public void Sanitise_WhenASelectorEscapesTheLayout_ShouldRefuse()
    {
        foreach (var selector in new[] { "body", ".a body", ".a, html", ".a ~ .b", ".a:hover", ".a:nth-child(0)", ".a:nth-child(21)", ".a:nth-child(2n)", ".a:first-child:last-child", ".a.", "> .a", ".a >", ".A", ".a .b .c .d .e" })
        {
            FirstRefusal($"{selector} {{ width: 1px; }}").ShouldBe(CustomLayoutErrorCodes.CSS_SELECTOR);
        }
    }

    [Test]
    public void Sanitise_WhenTheSelectorListIsTooLong_ShouldRefuse()
    {
        var nine = string.Join(", ", Enumerable.Range(0, 9).Select(i => $".a{i}"));

        FirstRefusal($"{nine} {{ width: 1px; }}").ShouldBe(CustomLayoutErrorCodes.CSS_SELECTOR);
        Pass($"{string.Join(", ", Enumerable.Range(0, 8).Select(i => $".a{i}"))} {{ width: 1px; }}").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenAllowedSelectorsAreUsed_ShouldWriteThemPrefixed()
    {
        var css = Pass(".a > .b:first-child, .c.d:nth-child(odd), .e .f:nth-child(20), .g:last-child, .h:nth-child(even) { width: 1px; }");

        css.ShouldStartWith($"{SCOPE}a > {SCOPE}b:first-child, {SCOPE}c{SCOPE}d:nth-child(odd), {SCOPE}e {SCOPE}f:nth-child(20), {SCOPE}g:last-child, {SCOPE}h:nth-child(even) {{\n");
    }

    [Test]
    public void Sanitise_WhenAClassLooksAlreadyScoped_ShouldPrefixItAgain()
    {
        Pass(".cl-0a1b2c3d-x { width: 1px; }").ShouldStartWith($"{SCOPE}cl-0a1b2c3d-x {{");
    }

    [Test]
    public void Sanitise_WhenPositionOrZIndexIsOutOfRange_ShouldRefuse()
    {
        FirstRefusal(".a { position: fixed; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { position: sticky; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { position: hidden; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { z-index: 11; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { z-index: 2147483647; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { z-index: var(--v); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        Pass(".a { position: absolute; z-index: 10; } .b { position: static; z-index: 0; display: none; }").ShouldContain("z-index: 10;");
    }

    [Test]
    public void Sanitise_WhenAnAtRuleIsUsedAnywhere_ShouldRefuse()
    {
        FirstRefusal("@charset x;").ShouldBe(CustomLayoutErrorCodes.CSS_AT_RULE);
        FirstRefusal(".a { width: 1px; }\n@namespace x;").ShouldBe(CustomLayoutErrorCodes.CSS_AT_RULE);
        FirstRefusal("@supports (a: b) { .a { width: 1px; } }").ShouldBe(CustomLayoutErrorCodes.CSS_AT_RULE);
    }

    [Test]
    public void Sanitise_WhenAVariableIsNotBound_ShouldRefuse()
    {
        FirstRefusal(".a { width: calc(var(--nope) * 1%); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { width: var(--v, 1px); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { width: var(); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { width: var(v); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        sanitiser.Sanitise(".a { width: var(--v); }", PREFIX, []).Errors[0].Code.ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
    }

    [Test]
    public void Sanitise_WhenATokenIsNotOnTheList_ShouldRefuse()
    {
        FirstRefusal(".a { color: var(--sw-nothing); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { color: var(--SW-INK); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { color: var(--app-bg); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
    }

    [Test]
    public void Sanitise_WhenTheShadowTokenIsUsed_ShouldRefuse()
    {
        FirstRefusal(".a { background: var(--sw-lift); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
        FirstRefusal(".a { box-shadow: 0 0 1px var(--sw-lift); }").ShouldBe(CustomLayoutErrorCodes.CSS_VARIABLE);
    }

    [Test]
    public void Sanitise_WhenRepeatIsOutsideAGridTemplate_ShouldRefuse()
    {
        FirstRefusal(".a { width: repeat(2, 1px); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { gap: minmax(0, 1px); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { grid-template-columns: repeat(13, 1fr); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { grid-template-columns: repeat(0, 1fr); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { grid-template-columns: repeat(2.5, 1fr); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        Pass(".a { grid-template-rows: repeat(12, minmax(0, 1fr)); }").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenAShadowHasFiftyLayers_ShouldRefuse()
    {
        var layers = string.Join(", ", Enumerable.Repeat("0 0 1px var(--sw-ink)", 50));
        var stops = string.Join(", ", Enumerable.Repeat("var(--sw-ink)", 17));
        var sixteen = string.Join(", ", Enumerable.Repeat("var(--sw-ink)", 16));

        FirstRefusal($".a {{ box-shadow: {layers}; }}").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal($".a {{ background: linear-gradient({stops}); }}").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        Pass($".a {{ background: linear-gradient({sixteen}); }}").ShouldNotBeNull();
        Pass($".a {{ box-shadow: {string.Join(", ", Enumerable.Repeat("0 0 1px var(--sw-ink)", 8))}; }}").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenNumbersAreTooLarge_ShouldRefuse()
    {
        FirstRefusal(".a { width: 1234567px; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 1.2345px; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 1.px; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 1e3px; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 10px20px; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        Pass(".a { width: 123456px; height: -0.125px; margin: .5em; }").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenTheUnitIsNotOnTheList_ShouldRefuse()
    {
        FirstRefusal(".a { width: 1rem; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 1vh; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { width: 1PX; }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
    }

    [Test]
    public void Sanitise_WhenAColourFunctionIsUsed_ShouldRefuseTheFunction()
    {
        FirstRefusal(".a { color: rgba(0,0,0,.5); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { color: color(display-p3 1 0 0); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a { color: light-dark(red, blue); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [Test]
    public void Sanitise_WhenColorMixIsNotTwoTokens_ShouldRefuse()
    {
        FirstRefusal(".a { color: color-mix(in srgb, red 50%, var(--sw-ink)); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { color: color-mix(in srgb, var(--v) 50%, var(--sw-ink)); }").ShouldBe(CustomLayoutErrorCodes.CSS_COLOUR);
        FirstRefusal(".a { color: color-mix(in srgb, var(--sw-ink) 150%, var(--sw-paper)); }").ShouldBe(CustomLayoutErrorCodes.CSS_COLOUR);
        FirstRefusal(".a { color: color-mix(in hsl, var(--sw-ink) 50%, var(--sw-paper)); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { color: color-mix(in srgb, var(--sw-ink), var(--sw-paper)); }").ShouldBe(CustomLayoutErrorCodes.CSS_COLOUR);
        Pass(".a { color: color-mix(in srgb, var(--sw-ink) 40%, var(--sw-paper)); background: transparent; border: 1px solid currentColor; }").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenRotateAndTranslateGetWrongArguments_ShouldRefuse()
    {
        FirstRefusal(".a { transform: rotate(10px); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { transform: rotate(1deg, 2deg); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { transform: rotate(); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { transform: translate(1deg); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        FirstRefusal(".a { transform: translate(1px, 2px, 3px); }").ShouldBe(CustomLayoutErrorCodes.CSS_VALUE);
        Pass(".a { transform: rotate(45deg) translate(10px, 50%); } .b { transform: translate(calc(var(--v) * 1px)); }").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenTheCssIsOversized_ShouldRefuseBeforeParsing()
    {
        var css = ".a { width: 1px; }" + new string(' ', 262_144);

        var (written, errors) = Run(css);

        written.ShouldBeNull();
        errors.ShouldHaveSingleItem().Code.ShouldBe(CustomLayoutErrorCodes.CSS_TOO_LARGE);
        Run(".a { width: 1px; }" + new string(' ', 262_144 - 18)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Sanitise_WhenTooManyRulesOrDeclarations_ShouldRefuse()
    {
        var rules = string.Concat(Enumerable.Range(0, 301).Select(i => $".a{i} {{ width: 1px; }}\n"));
        var declarations = ".a { " + string.Concat(Enumerable.Repeat("width: 1px; ", 41)) + "}";

        FirstRefusal(rules).ShouldBe(CustomLayoutErrorCodes.CSS_TOO_MANY_RULES);
        FirstRefusal(declarations).ShouldBe(CustomLayoutErrorCodes.CSS_TOO_MANY_RULES);
        Pass(string.Concat(Enumerable.Range(0, 300).Select(i => $".a{i} {{ width: 1px; }}\n"))).ShouldNotBeNull();
        Pass(".a { " + string.Concat(Enumerable.Repeat("width: 1px; ", 40)) + "}").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenParenthesesNestSevenDeep_ShouldRefuseAsAFunction()
    {
        FirstRefusal(".a { width: calc(" + new string('(', 6) + "1px" + new string(')', 6) + "); }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        Pass(".a { width: calc(" + new string('(', 5) + "1px" + new string(')', 6) + "; }").ShouldNotBeNull();
    }

    [Test]
    public void Sanitise_WhenFunctionsNestFiftyThousandDeep_ShouldRefuseWithoutOverflow()
    {
        var css = ".a { width: " + string.Concat(Enumerable.Repeat("calc(", 30_000)) + "1px" + new string(')', 30_000) + "; }";

        FirstRefusal(css).ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [Test]
    public void Sanitise_WhenParenthesesNestFiftyThousandDeep_ShouldRefuseWithoutOverflow()
    {
        var css = ".a { width: calc(" + new string('(', 50_000) + "1px" + new string(')', 50_000) + "); }";

        FirstRefusal(css).ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(new string('(', 50_000)).ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
        FirstRefusal(".a" + new string('(', 50_000) + " { }").ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [Test]
    public void Sanitise_WhenPassed_ShouldPrefixEveryClass()
    {
        var css = Pass(".a .b > .c:first-child, .d.e { width: 1px; } .f { height: 1px; }");

        var selectors = css.Split('\n').Where(l => l.StartsWith('.'));
        foreach (var line in selectors)
        {
            var classes = line.TrimEnd('{', ' ').Split([' ', ',', '>', ':'], StringSplitOptions.RemoveEmptyEntries).SelectMany(p => p.Split('.', StringSplitOptions.RemoveEmptyEntries));
            classes.Where(c => c is not ("first-child")).ShouldAllBe(c => c.StartsWith("cl-0a1b2c3d-bar-chart__", StringComparison.Ordinal));
        }

        css.ShouldNotContain(". ");
    }

    [TestCase("")]
    [TestCase("cl-0a1b2c3d-bar-chart")]
    [TestCase("cl-0a1b2c3-bar-chart__")]
    [TestCase("cl-0A1B2C3D-bar-chart__")]
    [TestCase("cl-0a1b2c3d-Bar__")]
    [TestCase("cl-0a1b2c3d-__")]
    [TestCase("cl-0a1b2c3d-bar-__")]
    [TestCase("cl-0a1b2c3d-bar--chart__")]
    [TestCase("x-0a1b2c3d-bar-chart__")]
    [TestCase("cl-0a1b2c3d-bar chart__")]
    public void Sanitise_WhenThePrefixIsMalformed_ShouldThrow(string prefix)
    {
        Should.Throw<ArgumentException>(() => sanitiser.Sanitise(".a { width: 1px; }", prefix, []));
    }

    [Test]
    public void Sanitise_WhenThePrefixIsTheUnnamedOne_ShouldAcceptIt()
    {
        sanitiser.Sanitise(".a { width: 1px; }", "cl-0a1b2c3d-x__", []).Css.ShouldBe(".cl-0a1b2c3d-x__a {\n  width: 1px;\n}\n");
    }

    [Test]
    public void Sanitise_WhenPassed_ShouldWriteTheSameBytesForTheSameInput()
    {
        const string CSS = ".a  >  .b{ width:calc( var(--p)*1% ) ;height : 10px }\n\n.c:nth-child(odd) {}";

        var first = sanitiser.Sanitise(CSS, PREFIX, Bound);
        var second = new CssSanitiser().Sanitise(CSS, PREFIX, Bound);

        first.Errors.ShouldBeEmpty();
        first.Css.ShouldBe(second.Css);
    }

    [Test]
    public void Sanitise_WhenRefused_ShouldReturnNoCss()
    {
        var (css, errors) = Run(".a { width: url(x); }");

        css.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Sanitise_WhenRefused_ShouldNotEchoTheInput()
    {
        const string SECRET = "zqxjkvsecretword";
        var attacks = new[]
        {
            $".a {{ {SECRET}: 1px; }}", $".a {{ width: {SECRET}; }}", $".a {{ width: {SECRET}(1); }}", $".{SECRET}[x] {{ }}", $"@{SECRET} x;",
            $".a {{ width: var(--{SECRET}); }}", $".a {{ color: #{SECRET}; }}", $".a {{ width: 1{SECRET}; }}", $".a {{ font-family: {SECRET}; }}",
            "/" + $"* {SECRET} " + "*/", $".a {{ width: \"{SECRET}\"; }}", $".a {{ --{SECRET}: 1; }}",
        };

        foreach (var attack in attacks)
        {
            var (css, errors) = Run(attack);
            css.ShouldBeNull();
            errors.ShouldNotBeEmpty();
            errors.ShouldAllBe(e => !e.Message.Contains(SECRET) && !e.Path.Contains(SECRET));
            errors.ShouldAllBe(e => e.Message == CustomLayoutErrorCodes.Text(e.Code));
        }
    }

    [Test]
    public void Sanitise_WhenTwoProblems_ShouldReturnBothWithTheirLines()
    {
        var (css, errors) = Run(".a {\n  width: 1px;\n  nope: 1px;\n}\n.b {\n  height: 1rem;\n}\n");

        css.ShouldBeNull();
        errors.Select(e => (e.Path, e.Code)).ShouldBe([("layout.css:3", CustomLayoutErrorCodes.CSS_PROPERTY), ("layout.css:6", CustomLayoutErrorCodes.CSS_VALUE)]);
    }

    [Test]
    public void Sanitise_WhenAFunctionIsRemovedFromTheAllowList_ShouldRefuseIt()
    {
        var allow = CustomLayoutAllow.Default with { CssFunctions = [.. CustomLayoutAllow.Default.CssFunctions.Where(f => f != "calc")] };
        const string CSS = ".a { width: calc(1px + 2px); }";

        Pass(CSS).ShouldNotBeNull();
        var (written, errors) = new CssSanitiser(allow).Sanitise(CSS, PREFIX, []);

        written.ShouldBeNull();
        errors.ShouldHaveSingleItem().Code.ShouldBe(CustomLayoutErrorCodes.CSS_FUNCTION);
    }

    [Test]
    public void Sanitise_WhenAPropertyIsRemovedFromTheAllowList_ShouldRefuseIt()
    {
        var allow = CustomLayoutAllow.Default with { CssProperties = [.. CustomLayoutAllow.Default.CssProperties.Where(p => p != "width")] };

        new CssSanitiser(allow).Sanitise(".a { width: 1px; }", PREFIX, []).Errors.ShouldHaveSingleItem().Code.ShouldBe(CustomLayoutErrorCodes.CSS_PROPERTY);
    }

    [Test]
    public void Sanitise_WhenTheSampleCssIsGiven_ShouldMatchTheCompiledSample()
    {
        var upload = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-upload.sample.json"))).RootElement;
        var compiled = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-compiled.sample.json"))).RootElement;

        var (css, errors) = sanitiser.Sanitise(upload.GetProperty("css").GetString()!, PREFIX, ["--v"]);

        errors.ShouldBeEmpty();
        css.ShouldBe(compiled.GetProperty("css").GetString());
    }

    [Test]
    public void Sanitise_WhenMonoFontTokenIsUsed_ShouldPass()
    {
        Pass(".a { font-family: var(--sw-font-mono); } .b { font-family: var(--sw-font-head); } .c { font-family: var(--sw-font-body); }")
            .ShouldContain("font-family: var(--sw-font-mono);");
    }

    [Test]
    public void Sanitise_WhenAnExtraColourTokenIsUsed_ShouldPass()
    {
        Pass(".a { background: var(--sw-extra-1); color: var(--sw-extra-1-ink); } .b { background: var(--sw-extra-4); color: var(--sw-extra-4-ink); }")
            .ShouldContain("background: var(--sw-extra-1);");
    }

    [Test]
    public void Sanitise_WhenWhiteSpacePreIsUsed_ShouldPass()
    {
        Pass(".a { white-space: pre; }").ShouldContain("white-space: pre;");
    }

    [Test]
    public void Sanitise_WhenAHatchIsARepeatingGradient_ShouldPass()
    {
        Pass(".a { background: repeating-linear-gradient(45deg, var(--sw-grid), var(--sw-grid) 4px, transparent 4px, transparent 8px); }")
            .ShouldContain("repeating-linear-gradient(45deg, var(--sw-grid), var(--sw-grid) 4px, transparent 4px, transparent 8px)");
    }

    [Test]
    public void Sanitise_WhenGivenTheDialCss_ShouldPass()
    {
        const string CSS = """
            .cols { display: grid; grid-template-columns: 820px 1fr; gap: 56px; align-items: center; }
            .dial { position: relative; width: 820px; height: 410px; }
            .arc { position: absolute; inset: 0; border-radius: 410px 410px 0 0;
              background: conic-gradient(from 270deg at 50% 100%, var(--sw-warn), var(--sw-accent) 90deg, var(--sw-accent-dark) 180deg, transparent 180deg);
              mask: radial-gradient(circle at 50% 100%, transparent 318px, var(--sw-ink) 320px); }
            .needle { position: absolute; left: 404px; bottom: 0; width: 12px; height: 330px; background: var(--sw-ink);
              transform-origin: 50% 100%; transform: rotate(calc((var(--v) - 50) * 1.8deg)); }
            .grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); }
            """;

        var (css, errors) = sanitiser.Sanitise(CSS, PREFIX, ["--v"]);

        errors.ShouldBeEmpty();
        css.ShouldNotBeNull().ShouldContain("conic-gradient(from 270deg at 50% 100%, var(--sw-warn), var(--sw-accent) 90deg, var(--sw-accent-dark) 180deg, transparent 180deg)");
    }

    [Test]
    public void Sanitise_WhenGivenEmptyCss_ShouldWriteNothing()
    {
        Pass(string.Empty).ShouldBeEmpty();
        Pass("  \n\n ").ShouldBeEmpty();
    }

    [Test]
    public void Sanitise_WhenTheCssIsRandomGarbage_ShouldNeverThrow()
    {
        var random = new Random(473);
        const string ALPHABET = ".{}():;,#@ \n-+*/>[]=%abcdeprxv0123456789\"'\\!<";
        for (var i = 0; i < 3000; i++)
        {
            var text = new string(Enumerable.Range(0, random.Next(1, 120)).Select(_ => ALPHABET[random.Next(ALPHABET.Length)]).ToArray());

            var (css, errors) = Run(text);

            (css is null).ShouldBe(errors.Count > 0);
        }
    }
}
