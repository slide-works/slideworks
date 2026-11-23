import { useEffect } from 'react';

export type PlayerKeyActions = {
  next(): void;
  previous(): void;
  first(): void;
  last(): void;
  fullscreen(): void;
};

// Keys belong to whatever the person is typing in: form fields, editable text
// and anything that says it is a text box.
function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) return false;
  return (
    target.closest(
      'input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"]',
    ) !== null
  );
}

// Space presses a focused button (Previous, Fullscreen, Copy link), so the
// player leaves it alone there.
function isButton(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest('button, summary, [role="button"]') !== null;
}

// The player's keyboard: ←/→, Space and Shift+Space, PageUp and PageDown,
// Home and End, and F for fullscreen. It returns before preventDefault while
// the person types or composes with an IME, leaves shortcuts that use a
// modifier to the browser, and ignores auto-repeats of a held key.
export function usePlayerKeys(actions: PlayerKeyActions): void {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      // keyCode 229 is the IME's own keydown in browsers that don't set isComposing on it.
      if (event.isComposing || event.keyCode === 229) return;
      if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
      if (isTyping(event.target)) return;
      if (event.key === ' ' && isButton(event.target)) return;

      const action = actionFor(event, actions);
      if (!action) return;
      event.preventDefault();
      // A held key (or a clicker that sends auto-repeats) is still swallowed,
      // but only its first keydown moves: one press, one step or slide.
      if (event.repeat) return;
      action();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [actions]);
}

function actionFor(event: KeyboardEvent, actions: PlayerKeyActions): (() => void) | null {
  switch (event.key) {
    case 'ArrowRight':
    case 'PageDown':
      return actions.next;
    case 'ArrowLeft':
    case 'PageUp':
      return actions.previous;
    case ' ':
      return event.shiftKey ? actions.previous : actions.next;
    case 'Home':
      return actions.first;
    case 'End':
      return actions.last;
    case 'f':
    case 'F':
      return actions.fullscreen;
    default:
      return null;
  }
}
