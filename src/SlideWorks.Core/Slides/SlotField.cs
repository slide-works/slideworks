namespace SlideWorks.Core.Slides;

// One named slot of a layout or of an object inside a slot. The names are frozen (plan section 4).
public sealed record SlotField(string Name, ISlotType Type, bool Required, string? Description = null);
