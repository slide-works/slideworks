import { useEffect, useLayoutEffect, useRef } from 'react';

// What the presenter window's keys do (P8). Each one is a call the page makes; none of them moves anything locally.
export type PresenterKeyActions = {
  next(): void;
  previous(): void;
  first(): void;
  last(): void;
  // A typed slide number, then Enter.
  jump(n: number): void;
  toggleNotes(): void;
  toggleOverview(): void;
  // Esc: closes whatever is open on top of the window. Closing twice is harmless.
  close(): void;
  toggleBlack(): void;
  toggleSkip(): void;
  resetTimer(): void;
  // Q: opens or closes the Questions panel (SW-9 D18). Given only where the window has the panel.
  questions?(): void;
};

// The poll's keys in the presenter window (SW-6 D19): P opens or closes the poll, R reveals the results, A shows the
// answer. Each is optional: it is given only while that action is available in the poll's phase, and a key whose action
// is missing does nothing. New round has no key; O and N stay SW-4's overview and notes.
export type PresenterPollKeyActions = {
  togglePoll?(): void;
  reveal?(): void;
  showAnswer?(): void;
};

// The longest slide number the window listens for.
const MAX_DIGITS = 4;

// Elements whose own keys win: typing in them never drives the talk (K12). A checkbox is not typing: it takes only Space.
function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target.isContentEditable ||
    (target instanceof HTMLInputElement && target.type !== 'checkbox') ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement
  );
}

// A button, link or checkbox activates itself with Space (and Enter), so those keys are left to it. A checkbox does not
// use the arrows, so they still move the talk (SW-668).
function activates(target: EventTarget | null): boolean {
  return (
    target instanceof HTMLButtonElement ||
    target instanceof HTMLAnchorElement ||
    (target instanceof HTMLInputElement && target.type === 'checkbox')
  );
}

// The presenter window's keys (P8): → Space PgDn next; ← PgUp previous; Home and End; a number then Enter jumps; N
// notes; O overview; Esc closes; B and . blackout; D skip deep dives; T timer reset; Q the Questions panel (SW-9). Keys
// are ignored in inputs, while composing and with a modifier held. While something is open on top (overview, phone
// dialog) only O, N and Esc work, so the overview's own keys never move the talk. Q is a toggle, so a held Q's repeats
// are ignored. actions may change on every render without re-subscribing; null
// (nothing to drive yet) ignores every key.
export function usePresenterKeys(actions: PresenterKeyActions | null, overlayOpen: boolean) {
  const latest = useRef({ actions, overlayOpen });
  // A layout effect, so the listener sees the new actions as soon as the render is on screen: a passive effect can run
  // after a key press that follows the paint, and that key would move from the position the page just left.
  useLayoutEffect(() => {
    latest.current = { actions, overlayOpen };
  }, [actions, overlayOpen]);
  const digits = useRef('');

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const { actions: act, overlayOpen: covered } = latest.current;
      if (!act || event.defaultPrevented || event.isComposing || event.key === 'Process') return;
      if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) return;

      const key = event.key;
      if (/^[0-9]$/.test(key) && !covered) {
        digits.current = (digits.current + key).slice(-MAX_DIGITS);
        event.preventDefault();
        return;
      }
      const typed = digits.current;
      digits.current = '';
      if (key === 'Enter' && typed !== '' && !covered) {
        event.preventDefault();
        act.jump(Number(typed));
        return;
      }

      if (event.repeat && key.toLowerCase() === 'q') return;
      const handler = handlerFor(key, act, covered, activates(event.target));
      if (!handler) return;
      event.preventDefault();
      handler();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}

// The poll's keys (SW-6 D19), for the poll panel, which sits under the poll control provider inside the presenter
// window. Same rules as the window's own keys: ignored in inputs, while composing (and the IME's Process key), with a
// modifier held, when something is open on top (overlayOpen) and when an earlier listener has handled the key. Unlike
// the window's keys, a held key's repeats are ignored. A key whose action is missing (not
// available in the poll's phase, or no poll on the slide) does nothing and is left alone. actions may change on every
// render without re-subscribing; null ignores every key.
export function usePresenterPollKeys(
  actions: PresenterPollKeyActions | null,
  overlayOpen: boolean,
) {
  const latest = useRef({ actions, overlayOpen });
  useLayoutEffect(() => {
    latest.current = { actions, overlayOpen };
  }, [actions, overlayOpen]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const { actions: act, overlayOpen: covered } = latest.current;
      // A held key repeats: the poll's keys are toggles, so only the first press counts (holding P must not open and
      // then close the poll).
      if (!act || covered || event.defaultPrevented || event.repeat) return;
      if (event.isComposing || event.key === 'Process') return;
      if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) return;
      const handler = pollHandlerFor(event.key, act);
      if (!handler) return;
      event.preventDefault();
      handler();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}

function pollHandlerFor(key: string, act: PresenterPollKeyActions): (() => void) | undefined {
  switch (key.toLowerCase()) {
    case 'p':
      return act.togglePoll;
    case 'r':
      return act.reveal;
    case 'a':
      return act.showAnswer;
  }
  return undefined;
}

// The open-response keys (SW-8 D14): P opens or closes, H hides the entry focused in the panel. R does nothing on a
// word cloud or an open-text slide, so it is taken and runs no action. New round has no key. On any other slide this
// hook is given null and P is left to do what it already did. A missing P or H (closed round, or no entry focused) is
// left alone. Repeats are ignored. actions may change on every render without re-subscribing; null ignores every key.
export type PresenterOpenKeyActions = {
  toggle?(): void;
  hide?(): void;
};

export function usePresenterOpenKeys(
  actions: PresenterOpenKeyActions | null,
  overlayOpen: boolean,
) {
  const latest = useRef({ actions, overlayOpen });
  useLayoutEffect(() => {
    latest.current = { actions, overlayOpen };
  }, [actions, overlayOpen]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const { actions: act, overlayOpen: covered } = latest.current;
      if (!act || covered || event.defaultPrevented || event.repeat) return;
      if (event.isComposing || event.key === 'Process') return;
      if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) return;
      const handler = openHandlerFor(event.key, act);
      if (!handler) return;
      event.preventDefault();
      handler();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}

function openHandlerFor(key: string, act: PresenterOpenKeyActions): (() => void) | undefined {
  switch (key.toLowerCase()) {
    case 'p':
      return act.toggle;
    case 'h':
      return act.hide;
    case 'r':
      return () => undefined;
  }
  return undefined;
}

function handlerFor(
  key: string,
  act: PresenterKeyActions,
  covered: boolean,
  onButton: boolean,
): (() => void) | undefined {
  switch (key.toLowerCase()) {
    case 'escape':
      return act.close;
    case 'o':
      return act.toggleOverview;
    case 'n':
      return act.toggleNotes;
  }
  if (covered) return undefined;
  switch (key) {
    case 'ArrowRight':
    case 'PageDown':
      return act.next;
    case ' ':
      return onButton ? undefined : act.next;
    case 'ArrowLeft':
    case 'PageUp':
      return act.previous;
    case 'Home':
      return act.first;
    case 'End':
      return act.last;
    case '.':
      return act.toggleBlack;
  }
  switch (key.toLowerCase()) {
    case 'b':
      return act.toggleBlack;
    case 'd':
      return act.toggleSkip;
    case 't':
      return act.resetTimer;
    case 'q':
      return act.questions;
  }
  return undefined;
}
