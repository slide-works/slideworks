import { useEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { GUEST_TEST_IDS } from './testIds';

const icon = (path: ReactNode) => (
  <svg
    aria-hidden
    viewBox="0 0 24 24"
    className="mt-0.5 size-5 shrink-0 text-(--sw-accent-glow)"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
  >
    {path}
  </svg>
);

const LINES: { text: string; icon: ReactNode }[] = [
  {
    text: 'No account and no name. We keep a random ID in this browser so you count once.',
    icon: icon(
      <>
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21c1.5-4 4.5-6 8-6s6.5 2 8 6" />
      </>,
    ),
  },
  {
    text: 'The presenter sees totals, not who answered what.',
    icon: icon(<path d="M4 20V10M10 20V4M16 20v-8M22 20H2" />),
  },
  {
    text: 'No ads, no trackers, no third-party scripts.',
    icon: icon(<path d="M12 3l8 3v6c0 4.5-3.4 8-8 9-4.6-1-8-4.5-8-9V6z" />),
  },
  {
    text: 'Your answers are kept with this session and deleted with it.',
    icon: icon(<path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13" />),
  },
];

// Opens from "Guest · Privacy" in the footer: four plain lines, no legal text. Focus goes to "Got it" on open; the page
// that opened it puts focus back on the footer button when onClose runs.
export function PrivacySheet({ onClose }: { onClose: () => void }) {
  const close = useRef<HTMLButtonElement>(null);
  useEffect(() => close.current?.focus(), []);

  // The only control is the close button, so Tab keeps focus on it.
  const keys = (event: KeyboardEvent) => {
    if (event.key === 'Escape') onClose();
    if (event.key === 'Tab') {
      event.preventDefault();
      close.current?.focus();
    }
  };

  return (
    <>
      <div aria-hidden onClick={onClose} className="fixed inset-0 z-30 bg-black/50" />
      <section
        data-testid={GUEST_TEST_IDS.privacySheet}
        role="dialog"
        aria-modal="true"
        aria-labelledby="guest-privacy-heading"
        onKeyDown={keys}
        className="fixed inset-x-0 bottom-0 z-40 mx-auto flex max-w-md flex-col gap-3 rounded-t-3xl border border-(--sw-night-line) bg-[#161d33] px-5 pt-4 pb-5 text-white"
      >
        <h2 id="guest-privacy-heading" className="font-head text-lg font-bold">
          You're a guest
        </h2>
        <ul className="m-0 flex list-none flex-col gap-3.5 p-0">
          {LINES.map((line) => (
            <li key={line.text} className="flex gap-3 text-sm text-(--sw-night-2)">
              {line.icon}
              <span>{line.text}</span>
            </li>
          ))}
        </ul>
        <button
          ref={close}
          type="button"
          data-testid={GUEST_TEST_IDS.privacyClose}
          aria-label="Got it, close the privacy sheet"
          onClick={onClose}
          className="mt-1 min-h-12 rounded-xl bg-(--sw-accent) px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
        >
          Got it
        </button>
      </section>
    </>
  );
}
