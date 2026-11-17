// A deck's parts as the client reads them (K19). The wire holds a part as its
// name alone or as an object; partsOf gives every caller the object.
export type DeckPart = {
  name: string;
  // The name the tracker shows; the name itself when the part has no short one.
  short: string;
  caption: string | null;
  // One or two characters for an unnumbered part, such as A; null for a numbered one.
  mark: string | null;
};

export type PartWire = string | DeckPart;

// Every part as an object. A string reads as a part with that name, its name
// as the short name, and no caption or mark, as the server reads a stored string.
export function partsOf(parts: readonly PartWire[]): DeckPart[] {
  return parts.map((part) =>
    typeof part === 'string' ? { name: part, short: part, caption: null, mark: null } : part,
  );
}
