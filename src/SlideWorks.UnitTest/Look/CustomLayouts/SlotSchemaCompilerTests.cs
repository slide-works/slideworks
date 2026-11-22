using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The slot schema stage (T10): a slots.json becomes a definition that validates slides as a built-in does,
// and every refusal is an error with a path and a declared code, never an exception.
public class SlotSchemaCompilerTests
{
    private static readonly SlotSchemaCompiler Compiler = new();

    private static string Doc(string slots, string head = "") =>
        $$"""{ "name": "my-layout", "description": "A layout for the tests.", "surface": "light", {{head}} "slots": { {{slots}} } }""";

    private static (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) Compile(string json, int fixedSteps = 0) =>
        Compiler.Compile(json, fixedSteps);

    private static IReadOnlyList<CustomLayoutError> Refused(string json) => Compile(json).Errors;

    private static int Steps(LayoutDefinition definition, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return definition.Steps!.Count(document.RootElement);
    }

    private static int ErrorsOf(LayoutDefinition definition, string slots, out SlotValidation validation)
    {
        using var document = JsonDocument.Parse(slots);
        validation = new SlotValidation();
        definition.Validate(document.RootElement, validation);
        return validation.ErrorCount;
    }

    [Test]
    public void Compile_WhenASlotIsLevels_ShouldUseTheBuiltInBlock()
    {
        var (definition, schema, errors) = Compile(Doc("""
            "headline": { "type": "text", "max": 30 }, "levels": { "type": "levels" }, "tag": { "type": "chip", "optional": true }
            """));

        errors.ShouldBeEmpty();
        schema!.Slots.Select(s => s.Type).ShouldBe(["text", "levels", "chip"]);
        var levels = (BlockSlot)definition!.Slots.Fields.Single(f => f.Name == "levels").Type;
        levels.Block.ShouldBeSameAs(InlineBlocks.Levels);
        ErrorsOf(definition, """{ "headline": "H", "levels": { "items": [ { "text": "a" } ] } }""", out var validation).ShouldBe(1);
        validation.Errors.Single().ShouldContain("my-layout.levels.items");
    }

    [Test]
    public void Compile_WhenStepsNamesAList_ShouldPlanOnePerItem()
    {
        var (definition, schema, errors) = Compile(Doc("""
            "rows": { "type": "list", "min": 1, "max": 6, "of": { "t": { "type": "text", "max": 5 } } }
            """, "\"steps\": \"rows\","));

        errors.ShouldBeEmpty();
        schema!.Steps.ShouldBe("rows");
        const string FOUR = """{ "rows": [ {"t":"a"}, {"t":"b"}, {"t":"c"}, {"t":"d"} ] """;
        Steps(definition!, FOUR + ", \"reveal\": \"one-by-one\" }").ShouldBe(4);
        Steps(definition!, FOUR + " }").ShouldBe(0);
    }

    [Test]
    public void Compile_WhenFixedStepsAreGiven_ShouldPlanThatMany()
    {
        var (definition, _, errors) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }"), 3);
        var (withoutSteps, _, _) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }"));

        errors.ShouldBeEmpty();
        Steps(definition!, """{ "t": "a", "reveal": "one-by-one" }""").ShouldBe(3);
        Steps(definition!, """{ "t": "a" }""").ShouldBe(0);
        withoutSteps!.Steps.ShouldBeNull();
        withoutSteps.Slots.Fields.ShouldNotContain(f => f.Name == "reveal");
    }

    [Test]
    public void Compile_WhenACalloutRevealsLast_ShouldAddOneStep()
    {
        var (definition, _, errors) = Compile(Doc("""
            "callout": { "type": "callout", "optional": true },
            "rows": { "type": "list", "min": 1, "max": 6, "of": { "t": { "type": "text", "max": 5 } } }
            """, "\"steps\": \"rows\","));

        errors.ShouldBeEmpty();
        Steps(definition!, """{ "rows": [ {"t":"a"}, {"t":"b"} ], "reveal": "one-by-one", "callout": { "tone": "accent", "text": "x", "reveal": "last" } }""").ShouldBe(3);
        Steps(definition!, """{ "rows": [ {"t":"a"}, {"t":"b"} ], "callout": { "tone": "accent", "text": "x", "reveal": "last" } }""").ShouldBe(1);
    }

    [Test]
    public void Compile_WhenAListHoldsAList_ShouldValidateTheInnerItems()
    {
        var (definition, _, errors) = Compile(FiveSchemas.HORIZON);

        errors.ShouldBeEmpty();
        ErrorsOf(
            definition!,
            """{ "headline": "H", "rows": [ { "label": "L", "segments": [ { "text": "t", "span": 500, "fill": "solid" } ] } ], "ticks": ["a", "b"] }""",
            out var validation).ShouldBe(1);
        validation.Errors.Single().ShouldStartWith("horizon.rows[0].segments[0].span");
    }

    [Test]
    public void Compile_WhenAListIsThreeDeep_ShouldRefuse()
    {
        var errors = Refused(Doc("""
            "a": { "type": "list", "min": 1, "max": 2, "of": { "b": { "type": "list", "min": 1, "max": 2, "of": {
              "c": { "type": "list", "min": 1, "max": 2, "of": { "d": { "type": "text", "max": 5 } } } } } } }
            """));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.a.of.b.of.c", CustomLayoutErrorCodes.SLOTS_DEPTH)]);
    }

    [Test]
    public void Compile_WhenAListHoldsText_ShouldValidateEachItem()
    {
        var (definition, schema, errors) = Compile(FiveSchemas.HORIZON);

        errors.ShouldBeEmpty();
        schema!.Slots.Single(s => s.Name == "ticks").Item!.Type.ShouldBe("text");
        ErrorsOf(
            definition!,
            """{ "headline": "H", "rows": [ { "label": "L", "segments": [ { "text": "t", "span": 5, "fill": "solid" } ] } ], "ticks": ["a", "this is far too long"] }""",
            out var validation).ShouldBe(1);
        validation.Errors.Single().ShouldStartWith("horizon.ticks[1]");
    }

    [TestCase("image")]
    [TestCase("levels")]
    [TestCase("chip")]
    public void Compile_WhenABlockIsInsideAList_ShouldRefuse(string type)
    {
        var optional = type == "image" ? ", \"optional\": true" : "";
        var errors = Refused(Doc($$"""
            "rows": { "type": "list", "min": 1, "max": 2, "of": { "x": { "type": "{{type}}"{{optional}} } } }
            """));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.rows.of.x", CustomLayoutErrorCodes.SLOTS_TYPE)]);
    }

    [Test]
    public void Compile_WhenTheNameIsABuiltIn_ShouldRefuse()
    {
        var errors = Refused(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }").Replace("my-layout", "bars", StringComparison.Ordinal));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_NAME)]);
        errors[0].Message.ShouldContain("(bars)");
    }

    [TestCase("bar-")]
    [TestCase("a--b")]
    [TestCase("a")]
    [TestCase("Bar")]
    [TestCase("1bar")]
    [TestCase("bar_chart")]
    [TestCase("abcdefghijklmnopqrstuvwxyzabcdef")]
    public void Compile_WhenTheNameIsNotALayoutName_ShouldRefuse(string name)
    {
        var errors = Refused(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }").Replace("my-layout", name, StringComparison.Ordinal));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_NAME)]);
        errors[0].Message.ShouldNotContain($"({name})");
    }

    [Test]
    public void Compile_WhenTheNameIsTheLongestAllowed_ShouldCompile()
    {
        var (_, schema, errors) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }").Replace("my-layout", "abcdefghijklmnopqrstuvwxyzabcde", StringComparison.Ordinal));

        errors.ShouldBeEmpty();
        schema!.Name.Length.ShouldBe(31);
    }

    [Test]
    public void Compile_WhenTheDescriptionIsMissing_ShouldRefuse()
    {
        var errors = Refused("""{ "name": "my-layout", "surface": "light", "slots": { } }""");

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_DESCRIPTION)]);
    }

    [Test]
    public void Compile_WhenTheDescriptionIsTooLong_ShouldRefuse()
    {
        var errors = Refused(Doc("").Replace("A layout for the tests.", new string('x', 201), StringComparison.Ordinal));

        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_DESCRIPTION]);
    }

    [Test]
    public void Compile_WhenATextHasNoMax_ShouldRefuse()
    {
        var errors = Refused(Doc("\"t\": { \"type\": \"text\" }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.t", CustomLayoutErrorCodes.SLOTS_LIMIT)]);
    }

    [TestCase("\"type\": \"text\", \"max\": 0")]
    [TestCase("\"type\": \"text\", \"max\": 501")]
    [TestCase("\"type\": \"text\", \"max\": 5.5")]
    [TestCase("\"type\": \"text\", \"max\": \"5\"")]
    [TestCase("\"type\": \"number\", \"min\": 5, \"max\": 5")]
    [TestCase("\"type\": \"number\", \"min\": 6, \"max\": 5")]
    [TestCase("\"type\": \"number\", \"min\": 0, \"max\": 1e300")]
    [TestCase("\"type\": \"enum\", \"values\": []")]
    [TestCase("\"type\": \"enum\", \"values\": [\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\"]")]
    [TestCase("\"type\": \"list\", \"min\": 0, \"max\": 13, \"of\": { \"type\": \"text\", \"max\": 5 }")]
    [TestCase("\"type\": \"list\", \"min\": 3, \"max\": 2, \"of\": { \"type\": \"text\", \"max\": 5 }")]
    [TestCase("\"type\": \"list\", \"min\": -1, \"max\": 2, \"of\": { \"type\": \"text\", \"max\": 5 }")]
    [TestCase("\"type\": \"list\", \"min\": 0, \"max\": 0, \"of\": { \"type\": \"text\", \"max\": 5 }")]
    [TestCase("\"type\": \"text\", \"max\": 5, \"optional\": \"yes\"")]
    public void Compile_WhenALimitIsOutOfRange_ShouldRefuseAtTheSlot(string slot)
    {
        var errors = Refused(Doc($"\"t\": {{ {slot} }}"));

        errors.ShouldNotBeEmpty();
        errors.ShouldAllBe(e => e.Path == "slots.t" && e.Code == CustomLayoutErrorCodes.SLOTS_LIMIT);
    }

    [Test]
    public void Compile_WhenANumberHasNoRange_ShouldRefuse()
    {
        var errors = Refused(Doc("\"n\": { \"type\": \"number\" }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.n", CustomLayoutErrorCodes.SLOTS_LIMIT)]);
    }

    [Test]
    public void Compile_WhenANumberRangeIsDecimal_ShouldValidateDecimals()
    {
        var (definition, schema, errors) = Compile(Doc("\"n\": { \"type\": \"number\", \"min\": 0.5, \"max\": 9.5 }"));

        errors.ShouldBeEmpty();
        ErrorsOf(definition!, """{ "n": 9.25 }""", out _).ShouldBe(0);
        ErrorsOf(definition!, """{ "n": 9.75 }""", out _).ShouldBe(1);
        (schema!.Slots[0].Min, schema.Slots[0].Max).ShouldBe((0.5, 9.5));
    }

    [Test]
    public void Compile_WhenANumberRangeIsFractionalWithinOneInteger_ShouldKeepTheDecimalBounds()
    {
        var (definition, schema, errors) = Compile(Doc("\"n\": { \"type\": \"number\", \"min\": 0.1, \"max\": 0.9 }"));

        errors.ShouldBeEmpty();
        (schema!.Slots[0].Min, schema.Slots[0].Max).ShouldBe((0.1, 0.9));
        ErrorsOf(definition!, """{ "n": 0.5 }""", out _).ShouldBe(0);
        ErrorsOf(definition!, """{ "n": 0.95 }""", out _).ShouldBe(1);
    }

    [Test]
    public void Compile_WhenThereAreManyInvalidlyNamedFields_ShouldReportTooManySlots()
    {
        var fields = string.Join(",", Enumerable.Range(0, 5_000).Select(i => $"\"Bad-{i}\":{{\"type\":\"text\",\"max\":5}}"));

        var errors = Refused(Doc(fields));

        errors.Select(e => e.Code).ShouldContain(CustomLayoutErrorCodes.SLOTS_TOO_MANY);
        errors.Count.ShouldBeLessThanOrEqualTo(21);
    }

    [Test]
    public void Compile_WhenThereAreFarTooManyFields_ShouldRefuseWithoutReadingThemAll()
    {
        var fields = string.Join(",", Enumerable.Range(0, 6_000).Select(i => $"\"f{i}\":{{\"type\":\"text\",\"max\":5}}"));

        var errors = Refused(Doc(fields));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_TOO_MANY)]);
    }

    [TestCase("\"values\": [\"a\", \"B\"]")]
    [TestCase("\"values\": [\"a\", \"has space\"]")]
    [TestCase("\"values\": [\"a\", \"1a\"]")]
    [TestCase("\"values\": [\"a\", \"a_b\"]")]
    [TestCase("\"values\": [\"a\", 5]")]
    [TestCase("\"values\": [\"a\", \"\"]")]
    public void Compile_WhenAnEnumValueIsNotAClassName_ShouldRefuse(string values)
    {
        var errors = Refused(Doc($"\"e\": {{ \"type\": \"enum\", {values} }}"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.e", CustomLayoutErrorCodes.SLOTS_LIMIT)]);
    }

    [Test]
    public void Compile_WhenAnEnumValueIsRepeated_ShouldRefuse()
    {
        var errors = Refused(Doc("\"e\": { \"type\": \"enum\", \"values\": [\"a\", \"b\", \"a\"] }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.e", CustomLayoutErrorCodes.SLOTS_LIMIT)]);
    }

    [TestCase("item")]
    [TestCase("reveal")]
    [TestCase("surface")]
    [TestCase("emphasis")]
    public void Compile_WhenASlotIsNamedItem_ShouldRefuse(string name)
    {
        var errors = Refused(Doc($"\"{name}\": {{ \"type\": \"text\", \"max\": 5 }}"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_NAME)]);
        errors[0].Message.ShouldEndWith($"({name})");
    }

    [TestCase("Big")]
    [TestCase("a-b")]
    [TestCase("1a")]
    [TestCase("a b")]
    public void Compile_WhenASlotNameIsMalformed_ShouldRefuseWithoutEchoingIt(string name)
    {
        var errors = Refused(Doc($"\"{name}\": {{ \"type\": \"text\", \"max\": 5 }}"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_NAME)]);
        errors[0].Message.ShouldBe(CustomLayoutErrorCodes.Text(CustomLayoutErrorCodes.SLOTS_NAME));
    }

    [Test]
    public void Compile_WhenAFieldNameIsReserved_ShouldRefuseAtTheList()
    {
        var errors = Refused(Doc("\"rows\": { \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": { \"item\": { \"type\": \"text\", \"max\": 5 } } }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.rows.of", CustomLayoutErrorCodes.SLOTS_NAME)]);
    }

    [Test]
    public void Compile_WhenAnImageIsRequired_ShouldRefuse()
    {
        var errors = Refused(Doc("\"pic\": { \"type\": \"image\" }, \"pic2\": { \"type\": \"image\", \"optional\": false }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe(
            [("slots.pic", CustomLayoutErrorCodes.SLOTS_LIMIT), ("slots.pic2", CustomLayoutErrorCodes.SLOTS_LIMIT)]);
    }

    [Test]
    public void Compile_WhenAnImageIsOptional_ShouldAcceptAnImageSlot()
    {
        var (definition, schema, errors) = Compile(Doc("\"pic\": { \"type\": \"image\", \"optional\": true }"));

        errors.ShouldBeEmpty();
        schema!.Slots.Single().Optional.ShouldBeTrue();
        definition!.Slots.Fields.Single().Type.ShouldBeOfType<ImageSlot>();
    }

    [Test]
    public void Compile_WhenAFieldIsNamedType_ShouldReadOfAsFields()
    {
        var (definition, schema, errors) = Compile(Doc("""
            "rows": { "type": "list", "min": 1, "max": 3, "of": { "type": { "type": "enum", "values": ["a", "b"] }, "text": { "type": "text", "max": 9 } } }
            """));

        errors.ShouldBeEmpty();
        schema!.Slots[0].Item.ShouldBeNull();
        schema.Slots[0].Fields.Select(f => f.Name).ShouldBe(["type", "text"]);
        ErrorsOf(definition!, """{ "rows": [ { "type": "a", "text": "x" } ] }""", out _).ShouldBe(0);
    }

    [Test]
    public void Compile_WhenOfHasAStringType_ShouldReadOfAsAScalar()
    {
        var (definition, schema, errors) = Compile(Doc("""
            "ticks": { "type": "list", "min": 1, "max": 3, "of": { "type": "enum", "values": ["a", "b"] } }
            """));

        errors.ShouldBeEmpty();
        schema!.Slots[0].Fields.ShouldBeEmpty();
        schema.Slots[0].Item!.Name.ShouldBe(CustomSlot.ITEM);
        ErrorsOf(definition!, """{ "ticks": ["a", "c"] }""", out var validation).ShouldBe(1);
        validation.Errors.Single().ShouldStartWith("my-layout.ticks[1]");
    }

    [TestCase("image")]
    [TestCase("list")]
    [TestCase("levels")]
    [TestCase("nope")]
    public void Compile_WhenAListHoldsANonScalarType_ShouldRefuse(string type)
    {
        var errors = Refused(Doc($"\"rows\": {{ \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": {{ \"type\": \"{type}\" }} }}"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.rows.of", CustomLayoutErrorCodes.SLOTS_TYPE)]);
    }

    [Test]
    public void Compile_WhenStepsNamesATextSlot_ShouldRefuse()
    {
        var errors = Refused(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }", "\"steps\": \"t\","));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_STEPS)]);
    }

    [Test]
    public void Compile_WhenStepsNamesAFieldOrNothing_ShouldRefuse()
    {
        var nested = Doc("\"rows\": { \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": { \"inner\": { \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": { \"type\": \"text\", \"max\": 5 } } } }", "\"steps\": \"inner\",");
        var number = Doc("\"t\": { \"type\": \"text\", \"max\": 5 }", "\"steps\": 3,");

        Refused(nested).Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_STEPS]);
        Refused(number).Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_STEPS]);
    }

    [TestCase("\"x\": { \"type\": \"video\" }")]
    [TestCase("\"x\": { \"type\": 5 }")]
    [TestCase("\"x\": { \"max\": 5 }")]
    [TestCase("\"x\": \"text\"")]
    [TestCase("\"x\": { \"type\": \"stamp\" }")]
    public void Compile_WhenTheTypeIsUnknown_ShouldRefuse(string slot)
    {
        var errors = Refused(Doc(slot));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.x", CustomLayoutErrorCodes.SLOTS_TYPE)]);
    }

    [Test]
    public void Compile_WhenACalloutHasAnotherName_ShouldRefuse()
    {
        var errors = Refused(Doc("\"note\": { \"type\": \"callout\" }"));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots.note", CustomLayoutErrorCodes.SLOTS_TYPE)]);
    }

    [Test]
    public void Compile_WhenAKeyIsUnknown_ShouldRefuse()
    {
        var atSlot = Refused(Doc("\"t\": { \"type\": \"text\", \"max\": 5, \"min\": 1 }"));
        var atDocument = Refused(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }", "\"colour\": 1,"));
        var inItem = Refused(Doc("\"r\": { \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": { \"type\": \"text\", \"max\": 5, \"optional\": true } }"));
        var onList = Refused(Doc("\"r\": { \"type\": \"list\", \"min\": 1, \"max\": 2, \"optional\": true, \"of\": { \"type\": \"text\", \"max\": 5 } }"));

        atSlot.Select(e => (e.Path, e.Code)).ShouldBe([("slots.t", CustomLayoutErrorCodes.SLOTS_KEY)]);
        atDocument.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_KEY)]);
        inItem.Select(e => (e.Path, e.Code)).ShouldBe([("slots.r.of", CustomLayoutErrorCodes.SLOTS_KEY)]);
        onList.Select(e => (e.Path, e.Code)).ShouldBe([("slots.r", CustomLayoutErrorCodes.SLOTS_KEY)]);
    }

    [Test]
    public void Compile_WhenTheJsonHasAComment_ShouldReturnOneError()
    {
        var errors = Refused("""{ "name": "my-layout" /* no */, "slots": {} }""");

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_MALFORMED)]);
    }

    [TestCase("")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("\"text\"")]
    [TestCase("{ \"name\": ")]
    [TestCase("{ \"a\": 1, \"a\": 2 }")]
    [TestCase("{ \"slots\": { \"t\": { \"type\": \"text\", \"max\": 5, \"max\": 6 } } }")]
    [TestCase("{ \"slots\": { \"t\": 1, }, }")]
    public void Compile_WhenTheJsonIsMalformed_ShouldReturnOneErrorAndNotThrow(string json)
    {
        var (definition, schema, errors) = Compile(json);

        definition.ShouldBeNull();
        schema.ShouldBeNull();
        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_MALFORMED)]);
    }

    [Test]
    public void Compile_WhenTheSlotsAreNotAnObject_ShouldRefuse()
    {
        var errors = Refused("""{ "name": "my-layout", "description": "d", "surface": "light", "slots": [] }""");

        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_MALFORMED]);
    }

    [Test]
    public void Compile_WhenTheSchemaIsOversized_ShouldRefuseBeforeParsing()
    {
        // Not JSON at all: only a size check can be what refuses it with this code.
        var errors = Refused(new string('x', 262_145));
        var atTheLimit = Refused(new string('x', 262_144));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_TOO_LARGE)]);
        atTheLimit.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_MALFORMED]);
    }

    [Test]
    public void Compile_WhenTheSizeIsCountedInBytes_ShouldRefuseMultiByteText()
    {
        var errors = Refused(new string('é', 131_073));

        errors.Select(e => e.Code).ShouldBe([CustomLayoutErrorCodes.SLOTS_TOO_LARGE]);
    }

    [Test]
    public void Compile_WhenTwoProblems_ShouldReturnBothPaths()
    {
        var errors = Refused(Doc("\"a\": { \"type\": \"text\" }, \"b\": { \"type\": \"video\" }"));

        errors.Select(e => e.Path).ShouldBe(["slots.a", "slots.b"]);
    }

    [Test]
    public void Compile_WhenMoreThanTwentyProblems_ShouldStopAtTwenty()
    {
        var slots = string.Join(",", Enumerable.Range(0, 30).Select(i => $"\"s{i}\": {{ \"type\": \"text\" }}"));

        var errors = Refused(Doc(slots));

        errors.Count.ShouldBe(20);
    }

    [Test]
    public void Compile_WhenThereAreTooManySlots_ShouldRefuse()
    {
        var fields = string.Join(",", Enumerable.Range(0, 16).Select(i => $"\"f{i}\": {{ \"type\": \"text\", \"max\": 5 }}"));
        var tooMany = Doc($"\"rows\": {{ \"type\": \"list\", \"min\": 1, \"max\": 2, \"of\": {{ {fields} }} }}, \"a\": {{ \"type\": \"text\", \"max\": 5 }}, "
            + "\"b\": { \"type\": \"text\", \"max\": 5 }, \"c\": { \"type\": \"text\", \"max\": 5 }, \"d\": { \"type\": \"text\", \"max\": 5 }, "
            + "\"e\": { \"type\": \"text\", \"max\": 5 }, \"f\": { \"type\": \"text\", \"max\": 5 }, \"g\": { \"type\": \"text\", \"max\": 5 }, "
            + "\"h\": { \"type\": \"text\", \"max\": 5 }, \"i\": { \"type\": \"text\", \"max\": 5 }");
        var exactly24 = Doc(string.Join(",", Enumerable.Range(0, 24).Select(i => $"\"s{i}\": {{ \"type\": \"text\", \"max\": 5 }}")));

        Refused(tooMany).Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_TOO_MANY)]);
        Refused(exactly24).ShouldBeEmpty();
    }

    [TestCase("light", LayoutSurface.Light)]
    [TestCase("dark", LayoutSurface.Dark)]
    [TestCase("either", LayoutSurface.Either)]
    public void Compile_WhenSurfaceIsEither_ShouldLetTheSlidePick(string word, LayoutSurface surface)
    {
        var (definition, schema, errors) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 5 }").Replace("\"light\"", $"\"{word}\"", StringComparison.Ordinal));

        errors.ShouldBeEmpty();
        definition!.Surface.ShouldBe(surface);
        schema!.Surface.ShouldBe(word);
    }

    [TestCase("\"surface\": \"light\"", "\"surface\": \"grey\"")]
    [TestCase("\"surface\": \"light\"", "\"surface\": 1")]
    [TestCase("\"surface\": \"light\", ", "")]
    public void Compile_WhenTheSurfaceIsWrong_ShouldRefuse(string from, string to)
    {
        var errors = Refused(Doc("").Replace(from, to, StringComparison.Ordinal));

        errors.Select(e => (e.Path, e.Code)).ShouldBe([("slots", CustomLayoutErrorCodes.SLOTS_SURFACE)]);
    }

    [Test]
    public void Validate_WhenATextIsOverItsMax_ShouldNameThePath()
    {
        var (definition, _, errors) = Compile(FiveSchemas.AGENT_LANES);

        errors.ShouldBeEmpty();
        const string SLOTS = """
            { "headline": "H", "lanes": [
              { "who": "a", "task": "t", "progress": 1, "state": "running", "status": "s" },
              { "who": "a", "task": "this task is far longer than the forty-four characters it may have", "progress": 1, "state": "waiting", "status": "s" } ],
              "levels": { "items": [ { "text": "a" }, { "text": "b" } ] } }
            """;
        ErrorsOf(definition!, SLOTS, out var validation).ShouldBe(1);
        validation.Errors.Single().ShouldBe("agent-lanes.lanes[1].task: at most 44 characters (got 66)");
    }

    [Test]
    public void Compile_WhenGivenTheSampleSchema_ShouldMatchTheCompiledSamplesName()
    {
        var upload = JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-upload.sample.json")))!;
        var compiled = JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-compiled.sample.json")))!;

        var (definition, schema, errors) = Compile(upload["slots"]!.ToJsonString());

        errors.ShouldBeEmpty();
        schema!.Name.ShouldBe(compiled["name"]!.GetValue<string>());
        definition!.Name.ShouldBe(compiled["name"]!.GetValue<string>());
        schema.Description.ShouldBe(compiled["description"]!.GetValue<string>());
    }

    [TestCase(FiveSchemas.AGENT_LANES, "agent-lanes")]
    [TestCase(FiveSchemas.DIAL, "dial")]
    [TestCase(FiveSchemas.CODE_MOCK, "code-mock")]
    [TestCase(FiveSchemas.CHAT_MOCK, "chat-mock")]
    [TestCase(FiveSchemas.HORIZON, "horizon")]
    public void Compile_WhenGivenEachOfTheFiveSchemas_ShouldCompile(string json, string name)
    {
        var (definition, schema, errors) = Compile(json);

        errors.ShouldBeEmpty();
        definition!.Name.ShouldBe(name);
        schema!.Name.ShouldBe(name);
    }

    [Test]
    public void Compile_WhenItRefuses_ShouldReturnNoDefinitionAndNoSchema()
    {
        var (definition, schema, errors) = Compile(Doc("\"t\": { \"type\": \"text\" }"));

        definition.ShouldBeNull();
        schema.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Compile_WhenRefusing_ShouldNeverEchoAPieceOfTheUpload()
    {
        var errors = Refused(Doc("\"zz-secret\": { \"type\": \"text\" }, \"t\": { \"type\": \"wow-secret\" }", "\"zz-key\": 1,"));

        errors.ShouldAllBe(e => !e.Message.Contains("secret", StringComparison.Ordinal) && !e.Path.Contains("secret", StringComparison.Ordinal) && !e.Message.Contains("zz-key", StringComparison.Ordinal));
    }
}
