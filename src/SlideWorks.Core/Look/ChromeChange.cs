namespace SlideWorks.Core.Look;

// Chrome overrides in a look change (D17). A null key is left as it is; a Change with a null Value
// resets that key to the template's default.
public sealed record ChromeChange(Change<bool?>? Tracker, Change<string?>? Footer, Change<bool?>? Progress);
