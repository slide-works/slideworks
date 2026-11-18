import { createContext, useContext } from 'react';
import type { QaControlView, QaSettingsPatch, QaSlidePatch } from '../types';

// What the Questions panel (presenter window), its settings strip, the strip on the phone remote and the deck page's
// settings card read (SW-9 Q9). Frozen after wave 0.2: the real provider (QaControlLive) and the fixture provider
// (QaControlFixture) both give this shape.
export type QaControlValue = {
  // The presenter's view of the deck's questions (Q6), or null before it has been read.
  control: QaControlView | null;
  // The seven moderation actions, each on a question by its number (1-based, as on the wire), and the two settings
  // changes. Each sends its route and settles once the answer is in, with the message for that call's own refusal or
  // failure, or null when it succeeded. It never rejects. A part that shows the outcome of its own call shows this
  // value, never `error` read after the await: `error` is one value for the whole provider, and another call can
  // clear it or set it meanwhile. A refused or failed call also sets `error`.
  approve(number: number): Promise<string | null>;
  hide(number: number): Promise<string | null>;
  restore(number: number): Promise<string | null>;
  answer(number: number): Promise<string | null>;
  reopen(number: number): Promise<string | null>;
  show(number: number): Promise<string | null>;
  unshow(number: number): Promise<string | null>;
  // Any of the deck's five settings.
  setSettings(patch: QaSettingsPatch): Promise<string | null>;
  // A slide's switch, by its 1-based number.
  setSlide(slide: number, patch: QaSlidePatch): Promise<string | null>;
  // The message for the last refused or failed call, or for a failed read or watch ("not_live" shown as words), or
  // null. Cleared by the next call.
  error: string | null;
};

// A provider's value, stamped with the deck it is for and the presenter's slide it follows (1-based, from SW-4's
// presentation state; null on the deck page). P1's real provider uses `slide` to re-read GET …/qa when the slide changes.
export type QaControlSource = { deckId: string; slide: number | null; value: QaControlValue };

export const QaControlContext = createContext<QaControlSource | null>(null);

// The presenter's Q&A controls for deckId. Only under a QaControlLive or QaControlFixture for that deck: anywhere else
// is a wiring mistake, so it throws rather than draw controls that do nothing.
export function useQaControl(deckId: string): QaControlValue {
  const source = useContext(QaControlContext);
  if (source === null) throw new Error('useQaControl needs a Q&A control provider');
  if (source.deckId !== deckId)
    throw new Error(`useQaControl(${deckId}) is under the provider for deck ${source.deckId}`);
  return source.value;
}
