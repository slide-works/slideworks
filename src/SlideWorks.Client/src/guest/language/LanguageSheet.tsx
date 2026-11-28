import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import type { GuestTranslations } from '../guestView';
import { GUEST_TEST_IDS } from '../testIds';

// The sheet the language chip opens (SW-883 D2): the deck's languages in their own names, each with its English name and
// how complete it is. A bottom sheet on a portrait phone and a side panel in landscape. Escape or the backdrop closes it
// and focus goes back to where it was opened from (or to the chip, when that has gone). A refused switch shows its
// message here and the sheet stays open.
export function LanguageSheet({
  translations,
  current,
  onPick,
  onClose,
}: {
  translations: GuestTranslations;
  current: string;
  onPick: (code: string) => Promise<void>;
  onClose: () => void;
}) {
  const section = useRef<HTMLElement>(null);
  const [failed, setFailed] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const opener = document.activeElement;
    section.current?.querySelector<HTMLElement>('[aria-pressed="true"]')?.focus();
    return () => {
      const target =
        opener instanceof HTMLElement && opener.isConnected
          ? opener
          : document.querySelector<HTMLElement>(`[data-testid="${GUEST_TEST_IDS.languageChip}"]`);
      target?.focus();
    };
  }, []);

  // Every way out is inert while a switch is in flight, so its failure message can't be lost with the sheet.
  const close = () => {
    if (!busy) onClose();
  };

  const pick = async (code: string) => {
    if (code === current) return onClose();
    setBusy(true);
    setFailed(false);
    try {
      await onPick(code);
      onClose();
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  // Tab stays inside the sheet.
  const keys = (event: KeyboardEvent) => {
    if (event.key === 'Escape') close();
    if (event.key !== 'Tab') return;
    const items = [...(section.current?.querySelectorAll<HTMLElement>('button') ?? [])];
    if (items.length === 0) return;
    const at = items.indexOf(document.activeElement as HTMLElement);
    const next = event.shiftKey ? (at <= 0 ? items.length - 1 : at - 1) : (at + 1) % items.length;
    event.preventDefault();
    items[next].focus();
  };

  return (
    <>
      <div aria-hidden onClick={close} className="fixed inset-0 z-30 bg-black/50" />
      <section
        ref={section}
        data-testid={GUEST_TEST_IDS.languageSheet}
        role="dialog"
        aria-modal="true"
        aria-labelledby="guest-language-heading"
        onKeyDown={keys}
        className="fixed inset-x-0 bottom-0 z-40 mx-auto flex max-h-[80dvh] max-w-md flex-col gap-3 overflow-y-auto rounded-t-3xl border border-(--sw-night-line) bg-[#161d33] px-5 pt-4 pb-5 text-white [@media(orientation:landscape)_and_(max-height:500px)]:inset-y-0 [@media(orientation:landscape)_and_(max-height:500px)]:right-0 [@media(orientation:landscape)_and_(max-height:500px)]:left-auto [@media(orientation:landscape)_and_(max-height:500px)]:mx-0 [@media(orientation:landscape)_and_(max-height:500px)]:max-h-none [@media(orientation:landscape)_and_(max-height:500px)]:w-80 [@media(orientation:landscape)_and_(max-height:500px)]:rounded-none [@media(orientation:landscape)_and_(max-height:500px)]:rounded-l-3xl"
      >
        <h2 id="guest-language-heading" className="font-head text-lg font-bold">
          Read the slides in
        </h2>
        <ul className="m-0 flex list-none flex-col gap-2 p-0">
          {translations.languages.map((language) => {
            const selected = language.code === current;
            return (
              <li key={language.code}>
                <button
                  type="button"
                  data-testid={GUEST_TEST_IDS.languageOption}
                  data-language={language.code}
                  aria-pressed={selected}
                  disabled={busy}
                  onClick={() => void pick(language.code)}
                  className={`flex min-h-12 w-full items-center gap-3 rounded-xl border px-3 py-2 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow) ${selected ? 'border-(--sw-accent-glow) bg-white/10' : 'border-(--sw-night-line) bg-white/5'}`}
                >
                  <span className="min-w-0 flex-1">
                    <span className="block text-base font-semibold">{language.name}</span>
                    <span className="block text-xs text-(--sw-night-muted)">
                      {language.englishName}
                    </span>
                  </span>
                  <span className="text-xs text-(--sw-night-2)">
                    {language.complete >= 100 ? 'Complete' : `${language.complete}% translated`}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
        {failed && (
          <p
            data-testid={GUEST_TEST_IDS.languageError}
            role="alert"
            className="text-sm text-[#fdba74]"
          >
            Couldn't switch language. Try again.
          </p>
        )}
      </section>
    </>
  );
}
