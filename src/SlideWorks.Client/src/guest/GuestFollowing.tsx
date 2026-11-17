import { useMemo, useRef } from 'react';
import { useParams } from 'react-router-dom';
import type { JoinView } from '../api/types';
import { CANVAS_HEIGHT, CANVAS_WIDTH, useCanvasScale } from '../player/useCanvasScale';
import { JoinProvider } from '../slides/JoinContext';
import { GuestQa } from '../qa/guest/GuestQa';
import { SlideRenderer } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import { ActivitySlot } from './ActivitySlot';
import type { AudienceCurrent, AudienceState } from './audienceEvents';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';
import type { AudienceStatus } from './useAudience';

// The slide on screen, drawn at the 1920×1080 canvas size and scaled to the width it is given, in the deck's look. A
// black screen is a plain black box. It is a picture: inert and hidden from assistive tech, named by the caption around it.
function MirroredSlide({
  view,
  current,
  dimmed,
}: {
  view: GuestView;
  current: AudienceCurrent;
  dimmed: boolean;
}) {
  const { code = '' } = useParams();
  const frame = useRef<HTMLDivElement>(null);
  const canvasFit = useCanvasScale(frame);
  // The guest gets no parts or deep dives (D15): a slide shows no part tag, and the footer author is the presenter.
  const deck = useMemo<SlideDeck>(
    () => ({
      title: view.deckTitle,
      author: view.presenterName,
      parts: [],
      look: view.look,
      deepDives: [],
    }),
    [view.deckTitle, view.presenterName, view.look],
  );

  // A guest is on the page because the talk is live, so the join slide draws as live: this page's own address and code, and
  // no joined count (guests never receive it, and the owner-only count must not read "0 joined").
  const join = useMemo<JoinView>(
    () => ({
      code,
      codeLabel: `${code.slice(0, 3)} ${code.slice(3)}`.trim(),
      joinUrl: `${window.location.origin}/j/${code}`,
      hostLabel: window.location.host,
      live: true,
    }),
    [code],
  );
  const slide =
    current.layout === 'join'
      ? { ...current, slots: { ...current.slots, count: 'hidden' as const } }
      : current;

  return (
    <div
      ref={frame}
      data-testid={GUEST_TEST_IDS.slide}
      data-slide={current.index}
      data-step={current.step}
      data-black={current.black}
      aria-hidden
      inert
      className={`relative aspect-video w-full shrink-0 overflow-hidden rounded-[10px] bg-black shadow-[0_10px_30px_rgb(0_0_0/.35)] outline outline-white/10 [@media(orientation:landscape)_and_(max-height:500px)]:w-[min(100cqw,calc(100cqh*16/9))] [@media(orientation:landscape)_and_(max-height:500px)]:rounded-none ${dimmed ? 'opacity-60' : ''}`}
    >
      {!current.black && (
        <div
          className="absolute top-0 left-0 overflow-hidden"
          style={{
            width: CANVAS_WIDTH,
            height: CANVAS_HEIGHT,
            transformOrigin: '0 0',
            transform: canvasFit ? `scale(${canvasFit.scale})` : undefined,
            visibility: canvasFit ? undefined : 'hidden',
          }}
        >
          <JoinProvider deckId="" join={join} owner={false}>
            <SlideRenderer
              slide={{ ...slide, number: current.index, part: null, deepDive: false }}
              deck={deck}
              total={current.count}
              step={current.step}
            />
          </JoinProvider>
        </div>
      )}
    </div>
  );
}

// The slide mirror with a progress strip, and the activity card below it. It fills the screen shell: no background of its
// own, so the night surface shows through. While the connection comes back the slide is dimmed and the chip says Offline.
// In landscape the slide fills the screen and the rest is left out, which helps people far from the projector.
export function GuestFollowing({
  view,
  state,
  status,
}: {
  view: GuestView;
  state: AudienceState;
  status: AudienceStatus;
}) {
  const { code = '' } = useParams();
  const current = state.current;
  const reconnecting = status === 'reconnecting';
  return (
    <main
      data-testid={GUEST_TEST_IDS.following}
      className="flex min-h-0 w-full max-w-full flex-1 flex-col gap-3 overflow-x-hidden px-4 pt-4 pb-3 [@media(orientation:landscape)_and_(max-height:500px)]:items-center [@media(orientation:landscape)_and_(max-height:500px)]:justify-center [@media(orientation:landscape)_and_(max-height:500px)]:overflow-hidden [@media(orientation:landscape)_and_(max-height:500px)]:h-dvh [@media(orientation:landscape)_and_(max-height:500px)]:p-0 [@media(orientation:landscape)_and_(max-height:500px)]:[container-type:size]"
    >
      <header className="flex items-center gap-3 [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
        <div className="min-w-0 flex-1">
          <h1
            data-testid={GUEST_TEST_IDS.deckTitle}
            className="truncate font-head text-[13px] leading-tight font-bold"
          >
            {view.deckTitle}
          </h1>
          <p
            data-testid={GUEST_TEST_IDS.presenter}
            className="truncate text-[11px] text-(--sw-night-muted)"
          >
            {view.presenterName}
          </p>
        </div>
        <span
          data-testid={GUEST_TEST_IDS.chip}
          className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${reconnecting ? 'bg-[rgb(234_88_12/.2)] text-[#fdba74]' : 'bg-[rgb(22_163_74/.18)] text-[#86efac]'}`}
        >
          <span
            aria-hidden
            className={`size-1.5 rounded-full ${reconnecting ? 'bg-[#fb923c]' : 'bg-[#4ade80]'}`}
          />
          {reconnecting ? 'Offline' : 'Live'}
        </span>
      </header>
      <GuestQa code={code} state={state} status={status} presenter={view.presenterName}>
        {current ? (
          <MirroredSlide view={view} current={current} dimmed={reconnecting} />
        ) : (
          <div
            data-testid={GUEST_TEST_IDS.waitingForPresenter}
            role="status"
            className="grid aspect-video w-full shrink-0 place-items-center rounded-[10px] border border-white/10 bg-black/30 text-sm text-(--sw-night-2) [@media(orientation:landscape)_and_(max-height:500px)]:w-[min(100cqw,calc(100cqh*16/9))] [@media(orientation:landscape)_and_(max-height:500px)]:rounded-none"
          >
            Waiting for the presenter
          </div>
        )}
        {current && (
          <div className="flex items-center gap-2.5 text-[11px] text-(--sw-night-muted) [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
            <span className="sr-only">{`Slide ${current.index} of ${current.count}`}</span>
            <p data-testid={GUEST_TEST_IDS.progress} aria-hidden className="tabular-nums">
              {`${current.index} / ${current.count}`}
            </p>
            <div aria-hidden className="h-[3px] flex-1 overflow-hidden rounded-full bg-white/10">
              <div
                className="h-full bg-(--sw-accent-glow)"
                style={{ width: `${(current.index / current.count) * 100}%` }}
              />
            </div>
          </div>
        )}
        <div className="flex min-h-0 flex-1 flex-col [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
          <ActivitySlot
            code={code}
            activity={state.activity}
            status={status}
            presenter={view.presenterName}
          />
        </div>
      </GuestQa>
    </main>
  );
}
