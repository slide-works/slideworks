import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { LiveChip } from '../live/LiveChip';
import { getPresenterView } from '../api/presenter';
import type { PresenterView } from '../api/types';
import {
  deepDivesAhead,
  nextPosition,
  nextPreview,
  previousPosition,
  type Position,
} from '../presentation/navigate';
import { deviceLabel } from '../presentation/deviceLabel';
import { Overview } from '../presentation/Overview';
import { PhoneConnectDialog } from '../presentation/PhoneConnectDialog';
import { NextPanel } from '../presentation/presenter/NextPanel';
import { NotesPanel } from '../presentation/presenter/NotesPanel';
import { NowPanel } from '../presentation/presenter/NowPanel';
import { PresenterTopBar } from '../presentation/presenter/PresenterTopBar';
import {
  usePresenterKeys,
  type PresenterKeyActions,
} from '../presentation/presenter/usePresenterKeys';
import type { PresentationState, PresentationUpdate, TimerCommand } from '../presentation/types';
import { usePresentation, type Presentation } from '../presentation/usePresentation';
import { OpenResponseControlLive } from '../openResponses/presenter/OpenResponseControlLive';
import { PresenterOpenPanel } from '../openResponses/presenter/PresenterOpenPanel';
import { PollControlLive } from '../polls/presenter/PollControlLive';
import { PollPanel } from '../polls/presenter/PollPanel';
import { QaControlLive } from '../qa/presenter/QaControlLive';
import { QaPanel } from '../qa/presenter/QaPanel';
import { QuestionsToolBar } from '../qa/presenter/QuestionsToolBar';
import { useDeckRealtime } from '../realtime/useDeckRealtime';
import { JoinProvider } from '../slides/JoinContext';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: PresenterView }
  | { kind: 'notFound' }
  | { kind: 'failed'; message: string };

// The notes' font size in pixels, kept per browser.
const NOTES_SIZE_KEY = 'slideworks.presenter.notesSize';
const NOTES_SIZE = { min: 12, max: 36, step: 2, initial: 18 };

const MOVE_FAILED = "Couldn't move the presentation. Check your connection and try again.";
const TIMER_FAILED = "Couldn't change the timer. Check your connection and try again.";

// /decks/:id/present, the presenter window (P8): signed in, with no Shell chrome. It joins the deck's presentation as
// the presenter and shows the slide the audience sees now, what comes next, the notes, the timer and the tools. It never
// keeps a position of its own: every key and button sends the new position with update, and the window renders the
// state the server sends back (D4).
export function PresenterPage() {
  const { id = '' } = useParams();
  // Computed once: a new label would rejoin the presentation.
  const [label] = useState(() => deviceLabel(navigator.userAgent, 'Browser'));
  const { load, refreshFailed } = usePresenterView(id);
  const presentation = usePresentation(id, 'presenter', label);
  const { status, state, clockMinutes } = presentation;
  const view = load.kind === 'loaded' ? load.view : null;

  const [error, setError] = useState('');
  const [notesShown, setNotesShown] = useState(true);
  const [notesSize, setNotesSize] = useState(readNotesSize);
  const [overviewOpen, setOverviewOpen] = useState(false);
  const [phoneOpen, setPhoneOpen] = useState(false);
  const [questionsOpen, setQuestionsOpen] = useState(false);
  const mainRef = useRef<HTMLElement>(null);
  const overviewButton = useRef<HTMLButtonElement>(null);
  const phoneButton = useRef<HTMLButtonElement>(null);
  const questionsButton = useRef<HTMLButtonElement>(null);

  const ready = view !== null && state !== null && clockMinutes !== null;
  const controls = usePresenterControls(view, state, presentation, setError);
  const closeOverview = useCallback(() => setOverviewOpen(false), []);
  const closePhone = useCallback(() => setPhoneOpen(false), []);
  const reopenAudience = useCallback(() => {
    if (view) window.open(view.audienceUrl, `sw-audience-${view.id}`);
  }, [view]);
  const changeNotesSize = (by: number) => {
    const next = Math.min(NOTES_SIZE.max, Math.max(NOTES_SIZE.min, notesSize + by));
    setNotesSize(next);
    saveNotesSize(next);
  };

  usePresenterKeys(
    controls && {
      ...controls.keys,
      toggleNotes: () => setNotesShown((shown) => !shown),
      toggleOverview: () => setOverviewOpen((open) => !open),
      questions: () => setQuestionsOpen((open) => !open),
      close: () => {
        setOverviewOpen(false);
        setPhoneOpen(false);
      },
    },
    overviewOpen || phoneOpen,
  );
  useFocusOnReady(ready, mainRef);
  useFocusOnClose(overviewOpen, overviewButton);
  useFocusOnClose(phoneOpen, phoneButton);
  useFocusOnPanelClose(questionsOpen, questionsButton);

  if (load.kind === 'notFound' || status === 'refused') {
    return (
      <main
        data-testid="presenter-page"
        data-deck-id={id}
        className="grid min-h-dvh content-center justify-items-center gap-2 bg-[#0a0e16] px-4 text-center text-[#e2e8f0]"
      >
        <h1 data-testid="presenter-not-found" className="page-title text-[#e2e8f0]">
          Deck not found
        </h1>
        <p className="text-meta text-[#94a3b8]">
          It may have been deleted, or it isn't yours to present.
        </p>
        <Link to="/decks" className="text-meta text-[#8ea5ff]">
          Back to your decks
        </Link>
      </main>
    );
  }

  if (!ready || !controls) {
    return (
      <main
        data-testid="presenter-page"
        data-deck-id={id}
        className="grid min-h-dvh content-center justify-items-center bg-[#0a0e16] px-4 text-[#e2e8f0]"
      >
        {load.kind === 'failed' ? (
          <p role="alert" data-testid="presenter-load-error" className="text-meta text-[#fca5a5]">
            {load.message}
          </p>
        ) : (
          <p data-testid="presenter-connecting" role="status" className="text-meta text-[#94a3b8]">
            Joining the presentation…
          </p>
        )}
      </main>
    );
  }

  const audienceClosed =
    status === 'live' && !state.connected.some((connection) => connection.role === 'audience');
  const message = error || (refreshFailed ? "Couldn't load the deck's latest changes." : '');

  // The presenter view is the owner's, so the window is the owner's: its slides know the deck's join, and its top bar
  // carries the live chip (SW-5 D19).
  return (
    <JoinProvider deckId={view.id} join={view.join} owner>
      <main
        ref={mainRef}
        data-testid="presenter-page"
        data-deck-id={id}
        tabIndex={-1}
        aria-label={`Presenting ${view.title}`}
        className="flex min-h-dvh flex-col bg-[#0a0e16] text-[13px] text-[#e2e8f0] outline-none"
      >
        <PresenterTopBar
          view={view}
          state={state}
          elapsedMs={presentation.elapsedMs}
          clockMinutes={clockMinutes}
          onTimer={controls.timer}
          onReopenAudience={reopenAudience}
          liveChip={<LiveChip deckTitle={view.title} joinBar={view.look.joinBar} compact />}
        />

        {status === 'reconnecting' && (
          <p
            data-testid="presenter-reconnecting"
            role="status"
            className="bg-[#b4580b]/25 px-4 py-2 text-sm text-[#fdba74]"
          >
            Reconnecting · the audience window won't follow until it's back
          </p>
        )}
        {audienceClosed && (
          <p
            data-testid="presenter-audience-closed"
            role="status"
            className="flex items-center gap-2 bg-white/5 px-4 py-2 text-sm"
          >
            Audience window closed ·
            <button
              type="button"
              data-testid="presenter-audience-reopen"
              aria-label="Reopen the audience window"
              className="font-semibold text-[#8ea5ff] underline focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
              onClick={reopenAudience}
            >
              Reopen
            </button>
          </p>
        )}
        {message && (
          <p
            data-testid="presenter-error"
            role="alert"
            className="bg-[#c0362c]/25 px-4 py-2 text-sm text-[#fca5a5]"
          >
            {message}
          </p>
        )}

        <QaControlLive deckId={id} slide={state.slide}>
          <div className="relative grid flex-1 content-start gap-4 p-4 lg:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)]">
            <NowPanel
              view={view}
              slide={state.slide}
              step={state.step}
              black={state.black}
              nextName={controls.nextName}
              previousName={controls.previousName}
              onNext={controls.keys.next}
              onPrevious={controls.keys.previous}
            />
            <div className="grid min-w-0 content-start gap-4">
              <NextPanel
                view={view}
                preview={controls.preview}
                deepDivesAhead={controls.deepDivesAhead}
                skipDeepDives={state.skipDeepDives}
              />
              <PollControlLive deckId={id} slide={state.slide}>
                <PollPanel deckId={id} overlayOpen={overviewOpen || phoneOpen} />
              </PollControlLive>
              <OpenResponseControlLive deckId={id} slide={state.slide}>
                <PresenterOpenPanel deckId={id} overlayOpen={overviewOpen || phoneOpen} />
              </OpenResponseControlLive>
              {questionsOpen && <QaPanel deckId={id} />}
              {notesShown && (
                <NotesPanel
                  notes={view.slides[state.slide - 1]?.notes ?? null}
                  size={notesSize}
                  onSmaller={() => changeNotesSize(-NOTES_SIZE.step)}
                  onLarger={() => changeNotesSize(NOTES_SIZE.step)}
                />
              )}
            </div>
            {overviewOpen && (
              <div className="absolute inset-0 z-10 overflow-auto bg-[#0a0e16]">
                <Overview
                  view={view}
                  current={state.slide}
                  skipDeepDives={state.skipDeepDives}
                  onJump={(n) => {
                    controls.keys.jump(n);
                    closeOverview();
                  }}
                  onClose={closeOverview}
                />
              </div>
            )}
          </div>

          <QuestionsToolBar
            deckId={id}
            overviewOpen={overviewOpen}
            notesShown={notesShown}
            skipDeepDives={state.skipDeepDives}
            black={state.black}
            phonesConnected={state.connected.filter((c) => c.role === 'phone').length}
            onOverview={() => setOverviewOpen((open) => !open)}
            onNotes={() => setNotesShown((shown) => !shown)}
            onSkip={controls.keys.toggleSkip}
            onBlack={controls.keys.toggleBlack}
            onPhone={() => setPhoneOpen(true)}
            questionsOpen={questionsOpen}
            onQuestions={() => setQuestionsOpen((open) => !open)}
            overviewRef={overviewButton}
            phoneRef={phoneButton}
            questionsRef={questionsButton}
          />
        </QaControlLive>
        <PhoneConnectDialog
          remoteUrl={view.remoteUrl}
          open={phoneOpen}
          onClose={closePhone}
          phones={state.connected.filter((c) => c.role === 'phone')}
        />
      </main>
    </JoinProvider>
  );
}

// GET /api/decks/{id}/presenter (P3), read again on every change to the deck so an agent's edit mid-talk shows in
// place. A failed re-read keeps the deck already shown and says so.
function usePresenterView(id: string): { load: Load; refreshFailed: boolean } {
  const [result, setResult] = useState<{ id: string; load: Load; refreshFailed: boolean } | null>(
    null,
  );
  const latest = useRef(0);

  const read = useCallback(() => {
    const seq = ++latest.current;
    const show = (load: Load) => {
      if (seq !== latest.current) return;
      setResult((current) =>
        load.kind === 'failed' && current?.id === id && current.load.kind === 'loaded'
          ? { ...current, refreshFailed: true }
          : { id, load, refreshFailed: false },
      );
    };
    getPresenterView(id).then(
      (view) => show({ kind: 'loaded', view }),
      (error: unknown) => {
        if (error instanceof ApiError && error.status === 404) show({ kind: 'notFound' });
        else
          show({
            kind: 'failed',
            message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
          });
      },
    );
  }, [id]);

  useEffect(() => {
    read();
    // Leaving the deck makes every read still in flight stale.
    return () => {
      latest.current += 1;
    };
  }, [read]);

  // Events only arrive from the join on, so a rejoin after a reconnect reads again; the first join doesn't need to.
  const joinedBefore = useRef(false);
  useEffect(() => {
    joinedBefore.current = false;
  }, [id]);
  const onJoined = useCallback(() => {
    if (joinedBefore.current) read();
    joinedBefore.current = true;
  }, [read]);
  useDeckRealtime(id, read, onJoined);

  return result?.id === id
    ? { load: result.load, refreshFailed: result.refreshFailed }
    : { load: { kind: 'loading' }, refreshFailed: false };
}

type Controls = {
  keys: Omit<PresenterKeyActions, 'toggleNotes' | 'toggleOverview' | 'close'>;
  timer(cmd: TimerCommand): void;
  preview: ReturnType<typeof nextPreview>;
  deepDivesAhead: number;
  nextName: string;
  previousName: string;
};

// Every move, worked out with the navigation rules (P5) from the state the server sent and sent back with update.
// A failed call shows a message; the next one that works clears it. null until there is a deck and a state.
function usePresenterControls(
  view: PresenterView | null,
  state: PresentationState | null,
  presentation: Presentation,
  setError: (message: string) => void,
): Controls | null {
  const { update, timer } = presentation;
  const slides = view?.slides;
  const stepCounts = useMemo(() => slides?.map((slide) => slide.steps) ?? [], [slides]);
  const deepDives = useMemo(
    () => new Set(slides?.filter((slide) => slide.deepDive).map((slide) => slide.number)),
    [slides],
  );
  if (!view || !state) return null;

  const pos: Position = { slide: state.slide, step: state.step };
  const skip = state.skipDeepDives;
  const total = view.slides.length;
  const send = (u: PresentationUpdate) =>
    void update(u).then(
      () => setError(''),
      () => setError(MOVE_FAILED),
    );
  const move = (to: Position) => {
    if (to.slide === pos.slide && to.step === pos.step) return;
    send({ ...to, black: state.black, skipDeepDives: skip });
  };
  const runTimer = (cmd: TimerCommand) =>
    void timer(cmd).then(
      () => setError(''),
      () => setError(TIMER_FAILED),
    );
  const preview = nextPreview(pos, stepCounts, deepDives, skip);
  const previous = previousPosition(pos, stepCounts, deepDives, skip);

  return {
    keys: {
      next: () => move(nextPosition(pos, stepCounts, deepDives, skip)),
      previous: () => move(previous),
      first: () => move({ slide: 1, step: 0 }),
      last: () => move({ slide: total, step: 0 }),
      jump: (n) => {
        if (Number.isInteger(n) && n >= 1 && n <= total) move({ slide: n, step: 0 });
      },
      toggleBlack: () => send({ ...pos, black: !state.black, skipDeepDives: skip }),
      toggleSkip: () => send({ ...pos, black: state.black, skipDeepDives: !skip }),
      resetTimer: () => runTimer('reset'),
    },
    timer: (cmd) => runTimer(cmd),
    preview,
    deepDivesAhead: deepDivesAhead(pos, deepDives),
    nextName: nextNameOf(view, preview),
    previousName:
      previous === pos ? 'Previous: this is the start' : `Previous: ${placeOf(view, previous)}`,
  };
}

function nextNameOf(view: PresenterView, preview: ReturnType<typeof nextPreview>): string {
  switch (preview.kind) {
    case 'step':
      return `Next: reveal ${preview.step} of ${view.slides[preview.slide - 1]?.steps ?? preview.step}`;
    case 'slide':
      return `Next: ${placeOf(view, { slide: preview.slide, step: 0 })}`;
    case 'end':
      return 'Next: this is the end of the deck';
  }
}

// "slide 9: The difference is when the customer gets to answer", or "slide 3, reveal 4".
function placeOf(view: PresenterView, pos: Position): string {
  const headline = view.slides[pos.slide - 1]?.headline;
  const slide = headline ? `slide ${pos.slide}: ${headline}` : `slide ${pos.slide}`;
  return pos.step > 0 ? `${slide}, reveal ${pos.step}` : slide;
}

// Focuses the window once it can be driven, so its keys work without a click.
function useFocusOnReady(ready: boolean, target: RefObject<HTMLElement | null>) {
  useEffect(() => {
    if (ready) target.current?.focus({ preventScroll: true });
  }, [ready, target]);
}

// Puts focus on the Questions tool when the panel closes, only if focus was lost with it (it was inside the panel and
// is now on the body). Focus on the page stays there, so Space still moves the talk instead of pressing the tool.
function useFocusOnPanelClose(open: boolean, tool: RefObject<HTMLElement | null>) {
  const wasOpen = useRef(open);
  useEffect(() => {
    const lost = document.activeElement === null || document.activeElement === document.body;
    if (wasOpen.current && !open && lost) tool.current?.focus();
    wasOpen.current = open;
  }, [open, tool]);
}

// Puts focus back on the button that opened something, once it closes.
function useFocusOnClose(open: boolean, opener: RefObject<HTMLElement | null>) {
  const wasOpen = useRef(open);
  useEffect(() => {
    if (wasOpen.current && !open) opener.current?.focus();
    wasOpen.current = open;
  }, [open, opener]);
}

function readNotesSize(): number {
  try {
    const size = Number(localStorage.getItem(NOTES_SIZE_KEY));
    return size >= NOTES_SIZE.min && size <= NOTES_SIZE.max ? size : NOTES_SIZE.initial;
  } catch {
    return NOTES_SIZE.initial;
  }
}

function saveNotesSize(size: number) {
  try {
    localStorage.setItem(NOTES_SIZE_KEY, String(size));
  } catch {
    // Storage is blocked (a private window): the size just isn't kept.
  }
}
