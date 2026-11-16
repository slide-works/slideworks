import { useState, type RefObject } from 'react';

export type CopyState = 'idle' | 'copied' | 'failed';

// Copies text to the clipboard for a "Copy" button. When the browser has no
// clipboard (plain http on a host other than localhost) or refuses the write,
// the state is 'failed' and the element in `selectOnFail` gets its text
// selected, so the person can copy it by hand ("Select and copy").
export function useCopyText(selectOnFail?: RefObject<HTMLElement | null>) {
  const [state, setState] = useState<CopyState>('idle');

  const fail = () => {
    setState('failed');
    const element = selectOnFail?.current;
    const selection = window.getSelection();
    if (!element || !selection) return;
    const range = document.createRange();
    range.selectNodeContents(element);
    selection.removeAllRanges();
    selection.addRange(range);
  };

  const copy = (text: string) => {
    // navigator.clipboard is undefined outside a secure context, whatever the
    // type declarations say.
    const clipboard = navigator.clipboard as Clipboard | undefined;
    if (!clipboard) {
      fail();
      return;
    }
    try {
      clipboard.writeText(text).then(() => setState('copied'), fail);
    } catch {
      fail();
    }
  };

  return { state, copy };
}
