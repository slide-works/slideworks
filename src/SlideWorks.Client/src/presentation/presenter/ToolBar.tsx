import type { ReactNode, Ref } from 'react';

type Props = {
  overviewOpen: boolean;
  notesShown: boolean;
  skipDeepDives: boolean;
  black: boolean;
  phonesConnected: number;
  onOverview(): void;
  onNotes(): void;
  onSkip(): void;
  onBlack(): void;
  onPhone(): void;
  // Where focus goes back to when the overview or the phone dialog closes.
  overviewRef?: Ref<HTMLButtonElement>;
  phoneRef?: Ref<HTMLButtonElement>;
};

// The presenter window's tool bar: Overview, Notes, Skip deep dives and Blackout, each with its key, and the phone
// button on the right. The toggles say whether they are on with aria-pressed.
export function ToolBar({
  overviewOpen,
  notesShown,
  skipDeepDives,
  black,
  phonesConnected,
  onOverview,
  onNotes,
  onSkip,
  onBlack,
  onPhone,
  overviewRef,
  phoneRef,
}: Props) {
  return (
    <nav
      aria-label="Presenter tools"
      className="flex flex-wrap items-center gap-2 border-t border-[#1f2937] px-4 pt-2.5 pb-3.5"
    >
      <Tool
        testId="presenter-tool-overview"
        pressed={overviewOpen}
        shortcut="O"
        onClick={onOverview}
        buttonRef={overviewRef}
      >
        Overview
      </Tool>
      <Tool testId="presenter-tool-notes" pressed={notesShown} shortcut="N" onClick={onNotes}>
        Notes
      </Tool>
      <Tool testId="presenter-tool-skip" pressed={skipDeepDives} shortcut="D" onClick={onSkip}>
        Skip deep dives
      </Tool>
      <Tool testId="presenter-tool-black" pressed={black} shortcut="B" onClick={onBlack}>
        Blackout
      </Tool>
      <button
        ref={phoneRef}
        type="button"
        data-testid="presenter-tool-phone"
        className="ml-auto inline-flex items-center gap-2 rounded-md border border-[#8ea5ff]/50 bg-white/5 px-3 py-1.5 text-[12.5px] font-medium hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
        onClick={onPhone}
      >
        {phonesConnected > 0
          ? `Phone connected${phonesConnected > 1 ? ` (${phonesConnected})` : ''}`
          : 'Use your phone as a remote'}
      </button>
    </nav>
  );
}

type ToolProps = {
  testId: string;
  pressed: boolean;
  shortcut: string;
  onClick(): void;
  buttonRef?: Ref<HTMLButtonElement>;
  children: ReactNode;
};

function Tool({ testId, pressed, shortcut, onClick, buttonRef, children }: ToolProps) {
  return (
    <button
      ref={buttonRef}
      type="button"
      data-testid={testId}
      aria-pressed={pressed}
      aria-keyshortcuts={shortcut}
      className="inline-flex items-center gap-2 rounded-md bg-white/5 px-3 py-1.5 text-[12.5px] font-medium hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-[#8ea5ff] aria-pressed:bg-[#8ea5ff]/20 aria-pressed:ring-1 aria-pressed:ring-[#8ea5ff]/60"
      onClick={onClick}
    >
      {children}
      <kbd
        aria-hidden
        className="rounded border border-[#334155] px-1.5 py-0.5 font-mono text-[10.5px] text-[#64748b]"
      >
        {shortcut}
      </kbd>
    </button>
  );
}
