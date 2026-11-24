namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// SW-768: the slides of the second-half rebuild that add_slide refuses with every text at its slot's maximum (23, 29, 40, and
// 44 when rebuilt as a two-column callout; 37, a 4x2 table, was refused at 120-character cells and fits since SW-767 made 80
// the cell maximum), each with the smallest single change that is accepted. A "lever" is one thing an agent
// changes: a slot's length, or one kind of decoration. Each slide's row holds three builds: at the maximum (refused, naming
// the slot in RefusedAt), with the lever (accepted, no warning) and, for a length lever, one step past it (still refused), so
// the number is the boundary and not a guess. The fit numbers behind the choice (px over the room, from the refusals) are in
// the SW-50 attachment "SW-50 rebuild check, slides 23–44". Salts follow SecondHalfSlides: the slide's number times 100 plus its place.
public static class RefusedSlides
{
    public sealed record Case(int Slide, string Layout, int Part, string Lever, string RefusedAt, Func<object> AtMaximum, Func<object> WithLever, Func<object>? OneStepPast = null);

    public static readonly IReadOnlyList<Case> ALL =
    [
        new(23, "cards", 5, "no badges and no icons (any one of the two, or any one text shortened alone, is still refused)", "cards.cards[0]",
            () => Cards(decorated: true), () => Cards(decorated: false)),
        new(29, "compare", 6, "both footers at 50 characters, not 90", "compare.failure",
            () => Compare(footer: 90), () => Compare(footer: 50), () => Compare(footer: 51)),
        new(40, "links", 7, "all four link sources at 85 characters, not 100", "links.links",
            () => Links(source: 100), () => Links(source: 85), () => Links(source: 86)),
        new(44, "two-column", 8, "callout text at 199 characters, not 220", "two-column.left",
            () => TwoColumn(callout: 220), () => TwoColumn(callout: 199), () => TwoColumn(callout: 200)),
    ];

    private static string M(int length, int salt) => MadeUpWords.Of(length, salt);

    private static object Cards(bool decorated) => new
    {
        kicker = M(60, 2301), headline = M(100, 2302),
        cards = Enumerable.Range(0, 3).Select(i => new
        {
            icon = decorated ? "check" : null, badge = decorated ? M(36, 2303 + 3 * i) : null,
            title = M(70, 2304 + 3 * i), text = M(140, 2305 + 3 * i), tone = "accent", fill = "plain",
        }).ToArray(),
        callout = new { tone = "accent", text = M(220, 2312) },
    };

    private static object ComparePanel(int salt, string icon, string tone, int footer) => new
    {
        label = M(24, salt), title = M(50, salt + 1), icon, tone, fill = "soft",
        rows = new[] { new { label = M(16, salt + 2), value = M(40, salt + 4) }, new { label = M(16, salt + 3), value = M(40, salt + 5) } },
        footer = M(footer, salt + 7), stamp = M(40, salt + 8),
    };

    private static object Compare(int footer) => new
    {
        kicker = M(60, 2901), headline = M(100, 2902),
        failure = ComparePanel(2903, "factory", "negative", footer),
        success = ComparePanel(2911, "people", "positive", footer),
    };

    private static object Links(int source) => new
    {
        kicker = M(60, 4001), headline = M(100, 4002),
        links = Enumerable.Range(0, 4).Select(i => new
        {
            kind = M(16, 4003 + 3 * i), title = M(90, 4004 + 3 * i), source = M(source, 4005 + 3 * i), url = $"https://northwind.example/{i}",
        }).ToArray(),
        qr = new { url = "https://northwind.example/more", caption = M(60, 4015) },
    };

    private static object TwoColumn(int callout) => new
    {
        kicker = M(60, 4401), headline = M(100, 4402),
        left = new { title = M(60, 4403), text = M(400, 4404) },
        right = new { title = M(60, 4405), text = M(400, 4406) },
        callout = new { tone = "accent", text = M(callout, 4407) },
    };
}
