using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// One slot as read from slots.json: the schema's slot (whole-number bounds) and the type slides are
// validated with (the bounds as written, which may be decimals).
internal sealed record ParsedSlot(CustomSlot Slot, ISlotType Type);
