import type { ReactNode, Ref } from 'react';

type Props = {
  skipDeepDives: boolean;
  black: boolean;
  disabled: boolean;
  onSlides(): void;
  onSkip(): void;
  onBlack(): void;
  // The deck's join bar (the QR code and the code on every slide), only while the deck is live: with no live session
  // there is no bar to show, so there is no button.
  joinBar?: { on: boolean; onToggle(): void };
  // The Slides button, so the page can give it focus back when the sheet closes.
  slidesRef?: Ref<HTMLButtonElement>;
};

function Action({
  testId,
  label,
  pressed,
  pressedClass = 'aria-pressed:bg-indigo-300/20 aria-pressed:text-indigo-100',
  disabled,
  onClick,
  buttonRef,
  children,
}: {
  testId: string;
  label: string;
  pressed?: boolean;
  pressedClass?: string;
  disabled: boolean;
  onClick(): void;
  buttonRef?: Ref<HTMLButtonElement>;
  children: ReactNode;
}) {
  return (
    <button
      ref={buttonRef}
      type="button"
      data-testid={testId}
      aria-pressed={pressed}
      disabled={disabled}
      onClick={onClick}
      className={`grid justify-items-center gap-1 rounded-xl bg-white/5 px-1 py-2 text-center text-[11px] leading-tight font-medium text-slate-400 focus-visible:outline-2 focus-visible:outline-indigo-300 disabled:opacity-40 ${pressedClass}`}
    >
      {children}
      {label}
    </button>
  );
}

// Slides (opens the slides sheet), Skip deep dives and Blackout, both toggles that show their pressed state, and Join
// code while the deck is live (SW-609): a toggle for the join bar on the room's screen.
export function ActionRow({
  skipDeepDives,
  black,
  disabled,
  onSlides,
  onSkip,
  onBlack,
  joinBar,
  slidesRef,
}: Props) {
  return (
    <div className={`grid gap-1.5 px-4 pb-4 ${joinBar ? 'grid-cols-4' : 'grid-cols-3'}`}>
      <Action
        testId="speaker-slides"
        label="Slides"
        disabled={disabled}
        onClick={onSlides}
        buttonRef={slidesRef}
      >
        <svg
          aria-hidden
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          className="size-4 text-slate-200"
        >
          <rect x="3" y="3" width="7" height="7" rx="1" />
          <rect x="14" y="3" width="7" height="7" rx="1" />
          <rect x="3" y="14" width="7" height="7" rx="1" />
          <rect x="14" y="14" width="7" height="7" rx="1" />
        </svg>
      </Action>
      <Action
        testId="speaker-skip"
        label="Skip deep dives"
        pressed={skipDeepDives}
        pressedClass="aria-pressed:bg-violet-500/30 aria-pressed:text-violet-200"
        disabled={disabled}
        onClick={onSkip}
      >
        <svg
          aria-hidden
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          className="size-4 text-slate-200"
        >
          <path d="M5 12h14M13 6l6 6-6 6" />
          <path d="M3 5v14" />
        </svg>
      </Action>
      <Action
        testId="speaker-blackout"
        label="Blackout"
        pressed={black}
        pressedClass="aria-pressed:bg-white aria-pressed:text-black"
        disabled={disabled}
        onClick={onBlack}
      >
        <svg aria-hidden viewBox="0 0 24 24" fill="currentColor" className="size-4">
          <rect x="3" y="5" width="18" height="14" rx="2" />
        </svg>
      </Action>
      {joinBar && (
        <Action
          testId="speaker-join-bar"
          label="Join code"
          pressed={joinBar.on}
          pressedClass="aria-pressed:bg-emerald-400/25 aria-pressed:text-emerald-100"
          disabled={disabled}
          onClick={joinBar.onToggle}
        >
          <svg
            aria-hidden
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            className="size-4 text-slate-200"
          >
            <rect x="3" y="3" width="7" height="7" rx="1" />
            <rect x="14" y="3" width="7" height="7" rx="1" />
            <rect x="3" y="14" width="7" height="7" rx="1" />
            <path d="M14 14h3v3h-3zM20 14v.01M20 20v.01M17 20v.01M20 17v.01" />
          </svg>
        </Action>
      )}
    </div>
  );
}
