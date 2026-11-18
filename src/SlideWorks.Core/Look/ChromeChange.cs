namespace SlideWorks.Core.Look;

// Chrome overrides in a look change (D17). A null key is left as it is; a Change with a null Value
// resets that key to the template's default. TopBar is "accent" or "none", or a Change with a null Value for the template's default. JoinBar (J6) has no template default, so it is a plain on or off, or left alone.
public sealed record ChromeChange(Change<bool?>? Tracker, Change<string?>? Footer, Change<bool?>? Progress, bool? JoinBar = null, Change<string?>? TopBar = null);
