namespace SlideWorks.AcceptanceTests.Journeys;

// The brief the Measure's agent builds from: "Ship Smaller", three parts, ten slides on ten different layouts. Slot text
// is ordinary sentence and Title Case prose, as an agent writes it. The image slide takes the id upload_image returns.
public static class TenSlideBrief
{
    public const string TITLE = "Ship Smaller";

    public static readonly string[] PARTS = ["The Cost", "Slice It", "Ship It"];

    // In the order the finished deck shows them.
    public static readonly string[] LAYOUTS =
        ["title", "divider", "bullets", "big-number", "image", "compare", "numbered-cards", "table", "chart", "close"];

    // The bullets slide reveals one point per step.
    public const int BULLETS_SLIDE = 3;
    public const int BULLETS_STEPS = 4;

    public const int IMAGE_SLIDE = 5;

    public static object Title => new
    {
        kicker = "Team Offsite 2026",
        title = "Ship Smaller",
        subtitle = "Why small batches beat big plans, and how to start on Monday",
        tagline = "One change a day beats one launch a quarter.",
        author = "Sam Rivera",
        affiliation = "Product Engineering",
        stamp = "Draft",
    };

    public static object Divider => new { title = "Big Batches Hide the Risk", sub = "Why the quarter-long launch keeps slipping." };

    public static object Bullets => new
    {
        kicker = "The Pattern",
        headline = "Every slip we had last year started as a big batch",
        items = new object[]
        {
            new { lead = "Risk shows up late.", text = "The first real customer sees it in week ten." },
            new { lead = "Merges get scary.", text = "Long branches collide, so people stop merging." },
            new { lead = "Feedback arrives as a verdict,", text = "not a nudge, when it is too late to change course." },
            new { lead = "Nobody can say how far along we are,", text = "because nothing is done until everything is." },
        },
        reveal = "one-by-one",
    };

    public static object BigNumber(string value) => new
    {
        kicker = "Last Year, Measured",
        headline = "It took us eleven weeks to get a change in front of a customer",
        value,
        caption = "Median from first commit to the first customer using it.",
        detail = "Eight of our twelve launches waited on one last piece of work that had nothing to do with the rest.",
        source = "Delivery data, January to October, twelve launches.",
    };

    public static object Image(string imageId) => new
    {
        kicker = "The Tool",
        headline = "A story map shows the thinnest release",
        image = new { image = imageId, alt = "A story map with the first release as a band across the top row." },
        points = new[] { "Across: the steps a customer takes", "Down: richer versions of each step", "Top row: the first release" },
        caption = "Draw it with the team before you split a single story.",
    };

    public static object Compare => new
    {
        kicker = "Same Feature, Two Ways",
        headline = "The difference is when the customer gets to answer",
        failure = new
        {
            label = "Failure Mode",
            title = "Slice by Layer",
            points = new[] { "Database, then API, then UI", "Nothing to show for five sprints", "The first feedback rewrites the API" },
        },
        success = new
        {
            label = "Success Mode",
            title = "Slice by Customer Action",
            points = new[] { "Save a card, then pay with a saved card", "Something to demo every week", "Feedback changes week two, not week ten" },
        },
    };

    public static object NumberedCards => new
    {
        kicker = "Before You Split a Story",
        headline = "Four questions, in this order",
        cards = new object[]
        {
            new { title = "Who Notices?", text = "Name the person who would see the change on day one." },
            new { title = "What Is the Thinnest Version?", text = "Hard-code anything that isn't the point." },
            new { title = "Can It Hide Behind a Flag?", text = "If not, what would have to change so it can?" },
            new { title = "What Would We Learn?", text = "If the answer is nothing, pick another slice." },
        },
    };

    public static object Table => new
    {
        kicker = "Sizing",
        headline = "Aim for slices a pair can ship inside a week",
        columns = new[] { "Slice Size", "Review Time", "Rollback", "Verdict" },
        rows = new object[]
        {
            new object[] { "One quarter", "Days", "Painful", new { text = "Avoid", tone = "bad" } },
            new object[] { "One sprint", "Hours", "Possible", new { text = "Fine", tone = "warn" } },
            new object[] { "Two to four days", "Under an hour", "Flip a flag", new { text = "Target", tone = "good" } },
        },
        highlightRow = 3,
    };

    public static object Chart => new
    {
        kicker = "Ship It",
        headline = "Lead time fell as the slices got smaller",
        kind = "bar",
        xLabels = new[] { "May", "Jun", "Jul", "Aug", "Sep", "Oct" },
        series = new[] { new { name = "Lead time", values = new[] { 11, 10, 8, 5, 3, 2 } } },
        yUnit = "wk",
        caption = "Weeks from first commit to the first customer. The median fell from eleven to two.",
        marker = new { x = 3.5, label = "Flags On" },
    };

    public static object Close => new
    {
        headline = "Pick one story.\nShip half of it by Friday.",
        sub = "Then tell us what you learned at the next demo.",
        author = "Sam Rivera, Product Engineering",
    };

    // A slide the agent adds by mistake and deletes again.
    public static object Aside => new { quote = "We stopped asking whether it was done and started asking whether it was out.", attribution = "Team Lead, Payments" };
}
