import { useCallback, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { DeckPageLook, DeckPageView } from '../api/types';
import { SharingDialog } from '../components/SharingDialog';
import { LookPanel } from '../look/LookPanel';
import { QaControlLive } from '../qa/presenter/QaControlLive';
import { QaSettingsCard } from '../qa/presenter/QaSettingsCard';
import { JoinProvider } from '../slides/JoinContext';
import { useDeckRealtime } from '../realtime/useDeckRealtime';
import { useReReadOnJoin } from '../realtime/useReReadOnJoin';
import { ActivityFeed } from './deck/ActivityFeed';
import { DeckHeader } from './deck/DeckHeader';
import { LaterOnThisPage } from './deck/LaterOnThisPage';
import { SelectedSlide } from './deck/SelectedSlide';
import { SlideGrid } from './deck/SlideGrid';
import { useSlideChecks, type Measure } from './deck/useSlideChecks';
import { measureSlide } from '../checks/measureSlide';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: DeckPageView }
  | { kind: 'notFound' }
  | { kind: 'failed'; message: string };

// /decks/:id: the deck page, from one GET /api/decks/{id} (DeckPageView): the
// slide grid as real thumbnails, the selected slide's notes and link, the
// activity log, Share and Present. It re-reads when the deck changes.
export function DeckPage({ measure = measureSlide }: { measure?: Measure }) {
  const { id = '' } = useParams();
  // Each read is stamped with its deck, so another deck's result never shows.
  const [result, setResult] = useState<{ id: string; load: Load } | null>(null);
  const load: Load = result?.id === id ? result.load : { kind: 'loading' };
  // The selected slide belongs to its deck: another deck starts at slide 1.
  const [selection, setSelection] = useState({ id, number: 1 });
  const selected = selection.id === id ? selection.number : 1;
  const [sharing, setSharing] = useState(false);

  // Latest wins: every read takes the next number, and only the newest read's
  // answer is shown, so a re-read (DeckChanged, a sharing save) that overlaps
  // another or outlives a move to another deck never lands an older snapshot.
  const latest = useRef(0);

  const read = useCallback(() => {
    const seq = ++latest.current;
    const show = (next: Load) => {
      if (seq === latest.current) setResult({ id, load: next });
    };
    return api.get<DeckPageView>(`/api/decks/${encodeURIComponent(id)}`).then(
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

  const reReadOnJoin = useReReadOnJoin(read, latest);

  useDeckRealtime(id, () => void read(), reReadOnJoin);

  const root = useRef<HTMLElement>(null);
  const checks = useSlideChecks(root, load.kind === 'loaded' ? load.view : null, measure);

  // A saved look shows at once; the DeckChanged re-read brings the new revision.
  const showLook = useCallback(
    (look: DeckPageLook) =>
      setResult((current) =>
        current?.id === id && current.load.kind === 'loaded'
          ? { id, load: { kind: 'loaded', view: { ...current.load.view, look } } }
          : current,
      ),
    [id],
  );

  if (load.kind === 'notFound') {
    return (
      <section
        data-testid="deck-page"
        className="grid justify-items-center gap-2 py-16 text-center"
      >
        <h2 data-testid="deck-not-found" className="page-title">
          Deck not found
        </h2>
        <p className="text-meta text-ink-muted">
          It may have been deleted, or it isn't yours to open.
        </p>
        <Link to="/decks" className="text-meta text-accent">
          Back to your decks
        </Link>
      </section>
    );
  }

  if (load.kind !== 'loaded') {
    return (
      <section data-testid="deck-page">
        {load.kind === 'failed' && (
          <p role="alert" data-testid="deck-error" className="panel px-4 py-3 text-meta text-bad">
            {load.message}
          </p>
        )}
      </section>
    );
  }

  const { view } = load;
  // A re-read can drop the selected slide; fall back to the last one left.
  const slide =
    view.slides.find((candidate) => candidate.number === selected) ?? view.slides.at(-1);

  return (
    <JoinProvider deckId={view.id} join={view.join} owner={view.canEdit}>
      <section ref={root} data-testid="deck-page" data-deck-id={view.id} className="grid gap-5">
        <DeckHeader view={view} onShare={() => setSharing(true)} />
        <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_18rem]">
          <SlideGrid
            view={view}
            findings={checks.bySlide}
            measured={checks.measured}
            selected={slide?.number ?? 0}
            onSelect={(number) => setSelection({ id, number })}
          />
          <div className="grid gap-4">
            {view.canEdit && <LookPanel deck={view} onSaved={showLook} />}
            {view.canEdit && (
              <QaControlLive deckId={view.id} slide={null}>
                <QaSettingsCard deckId={view.id} slide={slide?.number ?? null} />
              </QaControlLive>
            )}
            {slide && (
              <SelectedSlide
                slide={slide}
                findings={checks.bySlide.get(slide.number) ?? []}
                unchecked={checks.unchecked.has(slide.number)}
              />
            )}
            <ActivityFeed rows={view.activity} />
            <LaterOnThisPage />
          </div>
        </div>
        <SharingDialog
          deckId={view.id}
          open={sharing}
          onClose={() => setSharing(false)}
          onSaved={() => void read()}
        />
      </section>
    </JoinProvider>
  );
}
