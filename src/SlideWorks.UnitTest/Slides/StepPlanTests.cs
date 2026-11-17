using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class StepPlanTests
{
    private const string CALLOUT_LAST = """ "callout": { "tone": "accent", "text": "t", "reveal": "last" } """;

    [Test]
    public void Count_WhenStepCasesFile_ShouldMatchEachCase()
    {
        using var cases = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("step-cases.json")));

        cases.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
        foreach (var (item, index) in cases.RootElement.EnumerateArray().Select((item, index) => (item, index)))
        {
            SlideSteps.Count(item.GetProperty("layout").GetString()!, item.GetProperty("slots"))
                .ShouldBe(item.GetProperty("steps").GetInt32(), $"case {index}");
        }
    }

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldAddOneStep()
    {
        var plan = StepPlan.List("items");

        Count(plan, $$"""{ "items": ["a", "b"], "reveal": "one-by-one", {{CALLOUT_LAST}} }""").ShouldBe(3);
        Count(plan, $$"""{ "items": ["a", "b"], "reveal": "all", {{CALLOUT_LAST}} }""").ShouldBe(1);
        Count(plan, $$"""{ "items": ["a", "b"], {{CALLOUT_LAST}} }""").ShouldBe(1);
        Count(plan, """{ "items": ["a", "b"], "reveal": "one-by-one", "callout": { "tone": "accent", "text": "t", "reveal": "with-slide" } }""").ShouldBe(2);
        Count(plan, """{ "items": ["a", "b"], "reveal": "one-by-one", "callout": { "tone": "accent", "text": "t" } }""").ShouldBe(2);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        var plan = StepPlan.List("items").Then(StepPlan.Fixed(2, "the panels")).Then(StepPlan.When("detail", 1, "the detail"));

        Count(plan, """{ "items": ["a", "b"], "detail": "d", "reveal": "all" }""").ShouldBe(0);
        Count(plan, """{ "items": ["a", "b"], "detail": "d" }""").ShouldBe(0);
        Count(plan, """{ "items": ["a", "b"], "detail": "d", "reveal": null }""").ShouldBe(0);
    }

    [Test]
    public void Count_WhenRevealIsStrikeAndAnswer_ShouldCountAsOn()
    {
        Count(StepPlan.List("rows"), """{ "rows": [["a", "b"], ["c", "d"], ["e", "f"]], "reveal": "strike-and-answer" }""").ShouldBe(3);
    }

    [Test]
    public void Count_WhenFixed_ShouldBeThatManyWhateverTheSlots()
    {
        var plan = StepPlan.Fixed(1, "the success panel");

        Count(plan, """{ "reveal": "one-by-one" }""").ShouldBe(1);
        plan.Phrase.ShouldBe("1 for the success panel when `reveal` is `one-by-one`");
        plan.FirstList.ShouldBeNull();
    }

    [Test]
    public void Count_WhenSlotIsAbsentOrNull_ShouldSkipTheWhenSource()
    {
        var plan = StepPlan.When("failure.stamp", 1, "the stamp");

        Count(plan, """{ "failure": { "stamp": "Late" }, "reveal": "one-by-one" }""").ShouldBe(1);
        Count(plan, """{ "failure": { "title": "t" }, "reveal": "one-by-one" }""").ShouldBe(0);
        Count(plan, """{ "failure": { "stamp": null }, "reveal": "one-by-one" }""").ShouldBe(0);
        Count(plan, """{ "failure": "text", "reveal": "one-by-one" }""").ShouldBe(0);
        Count(plan, """{ "reveal": "one-by-one" }""").ShouldBe(0);
        plan.Phrase.ShouldBe("1 for the stamp when `failure.stamp` is set and `reveal` is `one-by-one`");
    }

    [Test]
    public void Count_WhenAnyOfTheSlotsIsSet_ShouldCountOnce()
    {
        var plan = StepPlan.WhenAny(["detail", "source.name"], 1, "the footer");

        Count(plan, """{ "detail": "d", "source": { "name": "n" }, "reveal": "one-by-one" }""").ShouldBe(1);
        Count(plan, """{ "source": { "name": "n" }, "reveal": "one-by-one" }""").ShouldBe(1);
        Count(plan, """{ "source": {}, "reveal": "one-by-one" }""").ShouldBe(0);
        plan.Phrase.ShouldBe("1 for the footer when `detail` or `source.name` is set and `reveal` is `one-by-one`");
    }

    [Test]
    public void Count_WhenListReversed_ShouldBeOnePerItem()
    {
        var plan = StepPlan.ListReversed("levels");

        Count(plan, """{ "levels": ["a", "b", "c"], "reveal": "one-by-one" }""").ShouldBe(3);
        Count(plan, """{ "reveal": "one-by-one" }""").ShouldBe(0);
        plan.Phrase.ShouldBe("one per `levels` item, last first, when `reveal` is `one-by-one`");
        plan.FirstList.ShouldBe("levels");
    }

    [Test]
    public void Count_WhenChainedWithThen_ShouldAddEverySource()
    {
        var plan = StepPlan.Fixed(1, "the right column").Then(StepPlan.List("points")).Then(StepPlan.When("detail", 2, "the detail"));

        Count(plan, $$"""{ "points": ["a", "b"], "detail": "d", "reveal": "one-by-one", {{CALLOUT_LAST}} }""").ShouldBe(6);
        Count(plan, """{ "points": ["a", "b"], "reveal": "one-by-one" }""").ShouldBe(3);
        plan.Phrase.ShouldBe("1 for the right column when `reveal` is `one-by-one`, then one per `points` item when `reveal` is `one-by-one`, "
            + "then 2 for the detail when `detail` is set and `reveal` is `one-by-one`");
        plan.FirstList.ShouldBe("points");
    }

    [Test]
    public void Count_WhenSlotsAreNotAnObject_ShouldBeZero()
    {
        Count(StepPlan.List("items"), "[]").ShouldBe(0);
        Count(StepPlan.List("items"), """{ "items": "text", "reveal": "one-by-one" }""").ShouldBe(0);
    }

    private static int Count(StepPlan plan, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return plan.Count(document.RootElement);
    }
}
