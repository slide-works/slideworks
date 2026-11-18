import type { Emphasis } from './slots/blocks';

// The value at a dotted path in the slots (`headline`, `failure.line`), when
// it is text.
function textAt(slots: object, target: string): string | undefined {
  let value: unknown = slots;
  for (const key of target.split('.')) {
    if (typeof value !== 'object' || value === null || !Object.hasOwn(value, key)) return undefined;
    value = (value as Record<string, unknown>)[key];
  }
  return typeof value === 'string' ? value : undefined;
}

// The emphasis entries of a slide that belong to one text slot (K10): those
// that name it with `in`, and those that name no slot and whose phrase is in
// this slot's text. The server has checked that exactly one slot holds such a
// phrase. `target` is a slot name or a dotted path into the slots.
export function emphasisFor(slots: object, target: string): Emphasis[] {
  const entries = (slots as { emphasis?: Emphasis[] }).emphasis;
  if (entries === undefined) return [];
  const text = textAt(slots, target);
  return entries.filter((entry) =>
    entry.in !== undefined
      ? entry.in === target
      : text !== undefined && entry.text !== '' && text.includes(entry.text),
  );
}

// A piece of a text: plain, or a phrase in its tone.
export type EmphasisPiece = { text: string; tone?: Emphasis['tone'] };

// Splits a text once per span, at the first place its phrase occurs (found as
// a plain string, never as a pattern). A phrase that is not in the text, or
// that starts inside an earlier one, is left as plain text. The pieces joined
// are always the text itself.
export function emphasisPieces(text: string, spans: readonly Emphasis[]): EmphasisPiece[] {
  const found = spans
    .map((span) => ({ start: span.text === '' ? -1 : text.indexOf(span.text), span }))
    .filter(({ start }) => start >= 0)
    .sort((a, b) => a.start - b.start);
  const pieces: EmphasisPiece[] = [];
  let at = 0;
  for (const { start, span } of found) {
    if (start < at) continue;
    if (start > at) pieces.push({ text: text.slice(at, start) });
    pieces.push({ text: span.text, tone: span.tone });
    at = start + span.text.length;
  }
  if (at < text.length || pieces.length === 0) pieces.push({ text: text.slice(at) });
  return pieces;
}
