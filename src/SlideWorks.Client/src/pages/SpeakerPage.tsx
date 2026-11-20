import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getPresenterView } from '../api/presenter';
import type { PresenterView } from '../api/types';
import {
  nextPosition,
  nextPreview,
  previousPosition,
  type Position,
} from '../presentation/navigate';
import type { PresentationState, PresentationUpdate } from '../presentation/types';
import { usePresentation, type Presentation } from '../presentation/usePresentation';
import { useDeckRealtime } from '../realtime/useDeckRealtime';
import { JoinProvider } from '../slides/JoinContext';
import { SlidesSheet } from '../remote/SlidesSheet';
import { PollControlLive } from '../polls/presenter/PollControlLive';
import { PollStrip } from '../polls/presenter/PollStrip';
import { QaControlLive } from '../qa/presenter/QaControlLive';
import { QaStrip } from '../qa/presenter/QaStrip';
import { ActionRow } from '../remote/speaker/ActionRow';
import { deviceLabel } from '../remote/speaker/deviceLabel';
import { NavButtons } from '../remote/speaker/NavButtons';
import { NextRow, nextLabel } from '../remote/speaker/NextRow';
import { NowSlide } from '../remote/speaker/NowSlide';
import { SpeakerNotes } from '../remote/speaker/SpeakerNotes';
import { StatusBar } from '../remote/speaker/StatusBar';
import { useWakeLock } from '../remote/speaker/useWakeLock';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: PresenterView }
  | { kind: 'notYours' }
  | { kind: 'failed'; message: string };

const NOT_YOURS = "This presentation isn't yours, or it doesn't exist";
const SEND_FAILED = "That didn't reach the talk, so nothing changed. Try again.";
const REFRESH_FAILED = "Couldn't load the deck's latest changes.";

const PAGE_CLASS = 'mx-auto flex h-dvh max-w-md flex-col bg-slate-950 text-slate-100';

// /remote/:id, the phone's speaker mode (P8): signed in, with no Shell chrome. One request (P3) for the deck and its
// notes, and the presentation (P6) joined as a phone. The slide comes first, the notes underneath (D6), then Next and
// Previous, Slides, Skip deep dives and Blackout. Every button sends a position worked out by navigate.ts (P5) and
// renders what the server sends back; nothing moves locally (D4).
export function SpeakerPage() {
  const { id = '' } = useParams();
  // Once per page: a new label would rejoin the presentation.
  const [label] = useState(() => deviceLabel(navigator.userAgent));
  const presentation = usePresentation(id, 'phone', label);
  const [result, setResult] = useState<{ id: string; load: Load } | null>(null);
  const load: Load = result?.id === id ? result.load : { kind: 'loading' };
  const [attempt, setAttempt] = useState(0);
  // The last re-read after the deck went live or ended failed: the talk stays on screen, with a message.
  const [refreshFailed, setRefreshFailed] = useState(false);

  useEffect(() => {
    let gone = false;
    getPresenterView(id).then(
      (view) => !gone && setResult({ id, load: { kind: 'loaded', view } }),
      (error: unknown) => {
        if (gone) return;
        setResult({
          id,
          load:
            error instanceof ApiError && error.status === 404
              ? { kind: 'notYours' }
              : {
                  kind: 'failed',
                  message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
                },
        });
      },
    );
    return () => {
      gone = true;
    };
  }, [id, attempt]);

  // The deck going live or ending elsewhere (the presenter window's chip) changes its join slide's state, so the view
  // is read again. A failed re-read keeps the talk on screen and says so; the next one that works clears the message.
  useDeckRealtime(id, (event) => {
    if (event.type !== 'liveChanged') return;
    getPresenterView(id).then(
      (view) => {
        setRefreshFailed(false);
        setResult({ id, load: { kind: 'loaded', view } });
      },
      () => setRefreshFailed(true),
    );
  });

  useWakeLock(presentation.status === 'live');

  if (load.kind === 'notYours' || presentation.status === 'refused') {
    return (
      <main data-testid="speaker-page" data-deck-id={id} className={PAGE_CLASS}>
        <p
          data-testid="speaker-not-yours"
          role="alert"
          className="m-auto px-6 text-center text-base"
        >
          {NOT_YOURS}
        </p>
      </main>
    );
  }
  if (load.kind === 'failed') {
    return (
      <main data-testid="speaker-page" data-deck-id={id} className={PAGE_CLASS}>
        <div className="m-auto grid gap-3 px-6 text-center">
          <p data-testid="speaker-load-failed" role="alert">
            Couldn't load this talk. {load.message}
          </p>
          <button
            type="button"
            data-testid="speaker-load-retry"
            className="btn-primary"
            onClick={() => setAttempt((n) => n + 1)}
          >
            Try loading the talk again
          </button>
        </div>
      </main>
    );
  }
  if (load.kind === 'loading' || presentation.state === null) {
    return (
      <main data-testid="speaker-page" data-deck-id={id} aria-busy className={PAGE_CLASS}>
        <p data-testid="speaker-connecting" role="status" className="m-auto text-slate-400">
          Joining the talk…
        </p>
      </main>
    );
  }
  // The phone is the owner's too, so a join slide on it shows the code and whether the deck is live (SW-5 D19).
  return (
    <JoinProvider deckId={load.view.id} join={load.view.join} owner>
      <PollControlLive deckId={id} slide={presentation.state.slide}>
        <QaControlLive deckId={id} slide={presentation.state.slide}>
          <SpeakerMode
            id={id}
            view={load.view}
            state={presentation.state}
            presentation={presentation}
            refreshFailed={refreshFailed}
          />
        </QaControlLive>
      </PollControlLive>
    </JoinProvider>
  );
}

function SpeakerMode({
  id,
  view,
  state,
  presentation,
  refreshFailed,
}: {
  id: string;
  view: PresenterView;
  state: PresentationState;
  presentation: Presentation;
  refreshFailed: boolean;
}) {
  const { status, elapsedMs } = presentation;
  const live = status === 'live';
  const [sheetOpen, setSheetOpen] = useState(false);
  const [error, setError] = useState('');
  const slidesRef = useRef<HTMLButtonElement>(null);
  const nextRef = useRef<HTMLButtonElement>(null);
  const previousRef = useRef<HTMLButtonElement>(null);
  const sheetRef = useRef<HTMLDivElement>(null);
  const reconnectingRef = useRef<HTMLDivElement>(null);
  // The last element in the page that had focus, to tell a focus the page took away from one the speaker moved.
  const lastFocused = useRef<Element | null>(null);
  // Focus goes where the last action leaves the speaker: into the sheet when it opens, back to its button when it closes.
  const focusAfter = useRef<'sheet' | 'slides' | 'next' | null>(null);

  const stepCounts = useMemo(() => view.slides.map((slide) => slide.steps), [view.slides]);
  const deepDives = useMemo(
    () => new Set(view.slides.filter((slide) => slide.deepDive).map((slide) => slide.number)),
    [view.slides],
  );
  const pos: Position = { slide: state.slide, step: state.step };
  const skip = state.skipDeepDives;
  const preview = nextPreview(pos, stepCounts, deepDives, skip);
  const next = nextPosition(pos, stepCounts, deepDives, skip);
  const previous = previousPosition(pos, stepCounts, deepDives, skip);
  // The buttons' subjects, with the slide's headline as the slides sheet names it: "slide 9: The difference is…".
  const withHeadline = (words: string, slide: number) => {
    const headline = view.slides[slide - 1]?.headline;
    return headline ? `${words}: ${headline}` : words;
  };
  const previousLabel =
    previous === pos
      ? null
      : withHeadline(
          previous.slide === pos.slide && previous.step > 0
            ? `reveal ${previous.step} of ${stepCounts[pos.slide - 1]}`
            : `slide ${previous.slide}`,
          previous.slide,
        );
  const nextButtonLabel =
    preview.kind === 'end' ? null : withHeadline(nextLabel(view, preview), preview.slide);
  const shown = view.slides[state.slide - 1];
  const part = shown?.part ? (view.parts[shown.part - 1]?.name ?? null) : null;
  // The sheet is only open while live: a jump while reconnecting would go nowhere.
  const showSheet = sheetOpen && live;

  useEffect(() => {
    const target = focusAfter.current;
    focusAfter.current = null;
    if (target === 'sheet') sheetRef.current?.querySelector('button')?.focus();
    else if (target === 'slides') slidesRef.current?.focus();
    else if (target === 'next') nextRef.current?.focus();
  }, [showSheet]);

  // When the focused control goes away or is disabled under the speaker (the end or start of the deck, a drop to
  // reconnecting, the sheet hiding or coming back with the connection), focus moves to what they can use now instead
  // of dropping to the page: into the sheet, to the reconnecting notice, or to whichever of Next and Previous works.
  useEffect(() => {
    const active = document.activeElement;
    const lost =
      (active instanceof HTMLButtonElement && active.disabled) ||
      (active === document.body &&
        lastFocused.current !== null &&
        (!lastFocused.current.isConnected ||
          (lastFocused.current instanceof HTMLButtonElement && lastFocused.current.disabled)));
    if (!lost) return;
    const target = showSheet
      ? sheetRef.current?.querySelector('button')
      : !live
        ? reconnectingRef.current
        : nextButtonLabel !== null
          ? nextRef.current
          : previousLabel !== null
            ? previousRef.current
            : null;
    target?.focus();
  });

  const send = (change: Partial<PresentationUpdate>) => {
    setError('');
    presentation
      .update({
        slide: state.slide,
        step: state.step,
        black: state.black,
        skipDeepDives: state.skipDeepDives,
        ...change,
      })
      .catch(() => setError(SEND_FAILED));
  };

  const onTimerTap = () => {
    setError('');
    presentation
      .timer(state.timer.state === 'running' ? 'pause' : 'resume')
      .catch(() => setError(SEND_FAILED));
  };

  const openSheet = () => {
    focusAfter.current = 'sheet';
    setSheetOpen(true);
  };
  const closeSheet = (focus: 'slides' | 'next') => {
    focusAfter.current = focus;
    setSheetOpen(false);
  };

  return (
    <main
      data-testid="speaker-page"
      data-deck-id={id}
      data-status={status}
      className={PAGE_CLASS}
      onFocus={(event) => {
        lastFocused.current = event.target;
      }}
    >
      {showSheet ? (
        <div ref={sheetRef} className="min-h-0 flex-1 overflow-auto">
          <SlidesSheet
            view={view}
            current={state.slide}
            skipDeepDives={skip}
            onJump={(n) => {
              send({ slide: n, step: 0 });
              closeSheet('next');
            }}
            onClose={() => {
              // A row calls onJump then onClose: keep the jump's focus target (Next).
              if (focusAfter.current !== 'next') closeSheet('slides');
            }}
          />
        </div>
      ) : (
        <>
          <StatusBar
            status={status}
            timerState={state.timer.state}
            elapsedMs={elapsedMs}
            talkMinutes={state.talkMinutes}
            slide={state.slide}
            total={view.slides.length}
            part={part}
            onTimerTap={onTimerTap}
          />
          {status !== 'live' && (
            <div
              ref={reconnectingRef}
              data-testid="speaker-reconnecting"
              role="status"
              tabIndex={-1}
              className="mx-4 mt-2 rounded-xl border border-red-300/40 bg-red-950/60 px-3 py-2.5 text-sm"
            >
              <b>Lost the connection to the laptop.</b> The laptop stays on slide {state.slide} and
              its keys still work. Your taps aren't sent until this reconnects; nothing queues up
              and fires later.
            </div>
          )}
          <div className="mt-2 grid gap-3">
            <NowSlide view={view} slide={state.slide} step={state.step} black={state.black} />
            <NextRow
              view={view}
              slide={state.slide}
              preview={preview}
              deepDives={deepDives}
              skipDeepDives={skip}
            />
          </div>
          <PollStrip deckId={id} />
          <QaStrip deckId={id} />
          <div className="mt-3 flex min-h-0 flex-1 flex-col">
            <SpeakerNotes view={view} slide={state.slide} step={state.step} lastKnown={!live} />
          </div>
          {(error || refreshFailed) && (
            <p data-testid="speaker-error" role="alert" className="mx-4 mt-2 text-sm text-red-300">
              {error || REFRESH_FAILED}
            </p>
          )}
          <NavButtons
            previous={previousLabel}
            next={nextButtonLabel}
            disabled={!live}
            onPrevious={() => send(previous)}
            onNext={() => send(next)}
            nextRef={nextRef}
            previousRef={previousRef}
          />
          <ActionRow
            skipDeepDives={skip}
            black={state.black}
            disabled={!live}
            onSlides={openSheet}
            onSkip={() => send({ skipDeepDives: !skip })}
            onBlack={() => send({ black: !state.black })}
            slidesRef={slidesRef}
          />
        </>
      )}
    </main>
  );
}
