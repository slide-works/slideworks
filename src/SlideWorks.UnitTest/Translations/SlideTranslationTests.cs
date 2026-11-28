using System.Text.Json;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// SlideTranslation.Merge (K3, D1, D18).
[TestFixture]
public class SlideTranslationTests
{
    private const string BULLETS = """
        { "headline": "Small steps win", "style": "numbered", "items": ["Ship daily", { "lead": "Merge.", "text": "Often." }],
          "emphasis": [{ "text": "Small steps", "tone": "accent" }] }
        """;

    [Test]
    public void Merge_WhenEntryCurrent_ShouldReplaceLeaf()
    {
        var merged = Merge(BULLETS, new() { ["/items/1/text"] = new("A menudo.", "Often."), ["/items/0"] = new("Publica cada día", "Ship daily") });

        merged.Slots.GetProperty("items")[0].GetString().ShouldBe("Publica cada día");
        merged.Slots.GetProperty("items")[1].GetProperty("text").GetString().ShouldBe("A menudo.");
        merged.Slots.GetProperty("items")[1].GetProperty("lead").GetString().ShouldBe("Merge.");
        merged.Slots.GetProperty("headline").GetString().ShouldBe("Small steps win");
        merged.FallbackLines.ShouldBe(2);
        merged.Lines.Single(line => line.Path == "/items/0").ShouldBe(new LineView("/items/0", "Ship daily", "Publica cada día", LineState.Current));
    }

    [Test]
    public void Merge_WhenEntryStale_ShouldKeepPrimaryAndCountFallback()
    {
        var merged = Merge(BULLETS, new() { ["/items/0"] = new("Publica cada semana", "Ship weekly") });

        merged.Slots.GetProperty("items")[0].GetString().ShouldBe("Ship daily");
        merged.FallbackLines.ShouldBe(4);
        merged.Lines.Single(line => line.Path == "/items/0").ShouldBe(new LineView("/items/0", "Ship daily", "Publica cada semana", LineState.Stale));
    }

    [Test]
    public void Merge_WhenEntryMissing_ShouldCountFallback()
    {
        using var document = JsonDocument.Parse(BULLETS);

        var merged = SlideTranslation.Merge(BulletsLayout.Definition, document.RootElement, null);

        merged.FallbackLines.ShouldBe(4);
        merged.Lines.Select(line => line.State).ShouldAllBe(state => state == LineState.Missing);
        merged.Lines.Select(line => line.Translation).ShouldAllBe(translation => translation == null);
        merged.Slots.GetRawText().ShouldBe(document.RootElement.GetRawText());
    }

    [Test]
    public void Merge_WhenEmphasisUnmatched_ShouldDropIt()
    {
        var merged = Merge(BULLETS, new() { ["/headline"] = new("Los pasos pequeños ganan", "Small steps win") });

        merged.Slots.TryGetProperty("emphasis", out _).ShouldBeFalse();
        merged.Slots.GetProperty("headline").GetString().ShouldBe("Los pasos pequeños ganan");
        merged.Lines.Single(line => line.Path == "/emphasis/0/text").State.ShouldBe(LineState.Missing);
    }

    [Test]
    public void Merge_WhenEmphasisMissing_ShouldNotCountFallback()
    {
        var merged = Merge(BULLETS, new() { ["/headline"] = new("Los pasos pequeños ganan", "Small steps win") });

        merged.Lines.Single(line => line.Path == "/emphasis/0/text").State.ShouldBe(LineState.Missing);
        merged.Lines.Count.ShouldBe(5);
        merged.FallbackLines.ShouldBe(3);
        TranslationCoverage.Of([merged]).ShouldBe(new LanguageCoverage(5, 1, 0, 4, 20));
    }

    [Test]
    public void Merge_WhenEmphasisMatched_ShouldKeepIt()
    {
        var merged = Merge(BULLETS, new()
        {
            ["/headline"] = new("Los pasos pequeños ganan", "Small steps win"),
            ["/emphasis/0/text"] = new("pasos pequeños", "Small steps"),
        });

        merged.Slots.GetProperty("emphasis")[0].GetProperty("text").GetString().ShouldBe("pasos pequeños");
        merged.Slots.GetProperty("emphasis")[0].GetProperty("tone").GetString().ShouldBe("accent");
    }

    [Test]
    public void Merge_WhenEmphasisUnmatchedAndNotDropping_ShouldKeepIt()
    {
        using var document = JsonDocument.Parse(BULLETS);
        var entries = new Dictionary<string, TranslationEntry> { ["/headline"] = new("Los pasos pequeños ganan", "Small steps win") };

        var merged = SlideTranslation.Merge(BulletsLayout.Definition, document.RootElement, entries, dropUnmatchedEmphasis: false);

        merged.Slots.GetProperty("emphasis")[0].GetProperty("text").GetString().ShouldBe("Small steps");
        merged.Slots.GetProperty("headline").GetString().ShouldBe("Los pasos pequeños ganan");
    }

    [Test]
    public void Merge_WhenPathIsNotALine_ShouldIgnoreIt()
    {
        var merged = Merge(BULLETS, new()
        {
            ["/style"] = new("numerado", "numbered"),
            ["/items/7"] = new("Nada", "Nothing"),
            ["/emphasis/0/tone"] = new("acento", "accent"),
        });

        merged.Slots.GetProperty("style").GetString().ShouldBe("numbered");
        merged.Slots.GetProperty("emphasis")[0].GetProperty("tone").GetString().ShouldBe("accent");
        merged.FallbackLines.ShouldBe(4);
        merged.Lines.Count.ShouldBe(5);
    }

    private static MergedSlide Merge(string slots, Dictionary<string, TranslationEntry> entries)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideTranslation.Merge(BulletsLayout.Definition, document.RootElement, entries);
    }
}
