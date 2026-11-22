using System.Text.Json;

namespace SlideWorks.AcceptanceTests.Pages;

// Measures one slide of the player from a single script over the whole slide, by test id only. Every slide stays
// rendered in the player, so a slide can be probed whether or not it is the one showing.
public sealed class SlideProbe(IPage page, int number)
{
    // Which of the frame's chrome parts are under the slide and drawn: in the layout, not visibility: hidden, with a box.
    private const string CHROME_PROBE =
        """
        slide => {
          const drawn = (id) => [...slide.querySelectorAll(`[data-testid="${id}"]`)].some((el) => {
            const box = el.getBoundingClientRect();
            return box.width > 0 && box.height > 0 && getComputedStyle(el).visibility !== 'hidden';
          });
          return { tracker: drawn('part-tracker'), footer: drawn('slide-footer'), journey: drawn('block-journey') };
        }
        """;

    private const string BOX_PROBE =
        """
        (slide, [id, nth]) => {
          const el = slide.querySelectorAll(`[data-testid="${id}"]`)[nth];
          if (!el) return null;
          const box = el.getBoundingClientRect();
          return { left: box.left, top: box.top, right: box.right, bottom: box.bottom };
        }
        """;

    private const string COMPUTED_PROBE =
        """
        (slide, [id, property, nth]) => {
          const el = slide.querySelectorAll(`[data-testid="${id}"]`)[nth];
          return el ? getComputedStyle(el).getPropertyValue(property) : null;
        }
        """;

    private const string TAG_PROBE =
        """
        (slide, [id, nth]) => {
          const el = slide.querySelectorAll(`[data-testid="${id}"]`)[nth];
          return el ? el.tagName.toLowerCase() : null;
        }
        """;

    private ILocator Slide => page.GetByTestId($"slide-{number}");

    // Which of the tracker, the footer and the journey the slide shows.
    public async Task<ChromeParts> ChromeAsync()
    {
        var found = await Slide.EvaluateAsync<JsonElement>(CHROME_PROBE);
        return new ChromeParts(found.GetProperty("tracker").GetBoolean(), found.GetProperty("footer").GetBoolean(), found.GetProperty("journey").GetBoolean());
    }

    // The nth element with this test id under the slide, in viewport pixels; null when there is none.
    public async Task<Box?> BoxAsync(string testId, int nth = 0)
    {
        var found = await Slide.EvaluateAsync<JsonElement>(BOX_PROBE, new object[] { testId, nth });
        return found.ValueKind == JsonValueKind.Null
            ? null
            : new Box(found.GetProperty("left").GetDouble(), found.GetProperty("top").GetDouble(), found.GetProperty("right").GetDouble(), found.GetProperty("bottom").GetDouble());
    }

    // The box once two reads in a row agree within half a pixel (layout may still be settling); null when there is none.
    public async Task<Box?> SettledBoxAsync(string testId, int nth = 0)
    {
        Box? previous = null;
        return await Journeys.Eventually.ReadAsync(
            () => BoxAsync(testId, nth),
            box =>
            {
                var same = box is not null && previous is not null && box.IsNear(previous);
                previous = box;
                return same;
            });
    }

    // Every element with this test id under the slide, in document order.
    public async Task<IReadOnlyList<Box>> BoxesAsync(string testId)
    {
        var found = new List<Box>();
        while (await BoxAsync(testId, found.Count) is { } box)
        {
            found.Add(box);
        }
        return found;
    }

    // The computed value of a CSS property on the nth element with this test id; null when there is none.
    public async Task<string?> ComputedAsync(string testId, string property, int nth = 0)
    {
        var found = await Slide.EvaluateAsync<JsonElement>(COMPUTED_PROBE, new object[] { testId, property, nth });
        return found.ValueKind == JsonValueKind.Null ? null : found.GetString();
    }

    // The tag name of the nth element with this test id; null when there is none.
    public async Task<string?> TagAsync(string testId, int nth = 0)
    {
        var found = await Slide.EvaluateAsync<JsonElement>(TAG_PROBE, new object[] { testId, nth });
        return found.ValueKind == JsonValueKind.Null ? null : found.GetString();
    }

    // The frame's data-surface: "light" or "dark".
    public Task<string?> SurfaceAsync() => Slide.GetByTestId("slide-frame").GetAttributeAsync("data-surface");

    public sealed record ChromeParts(bool Tracker, bool Footer, bool Journey);

    public sealed record Box(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;

        // Same x, y, width and height within the tolerance (half a pixel by default).
        public bool IsNear(Box other, double tolerance = 0.5) =>
            Math.Abs(Left - other.Left) <= tolerance && Math.Abs(Top - other.Top) <= tolerance
            && Math.Abs(Width - other.Width) <= tolerance && Math.Abs(Height - other.Height) <= tolerance;
    }
}
