import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import type { DeckPlayView } from '../api/types';
import { deepDivesOf } from '../look/deepDivesOf';
import { fontsSettled } from '../look/fontsSettled';
import { measureSlide } from '../checks/measureSlide';
import type { SlideFinding } from '../checks/types';
import { JoinProvider } from '../slides/JoinContext';
import { SlideRenderer } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';

const TOKEN_HEADER = 'X-Render-Token';
const TOKEN_MISSING = 'Render token missing';

type Load =
  | { state: 'loading' }
  | { state: 'loaded'; view: DeckPlayView }
  | { state: 'failed'; error: string };

// The API's { error } text, or the status when the body has none.
async function errorOf(response: Response): Promise<string> {
  const body = (await response.json().catch(() => null)) as { error?: unknown } | null;
  return typeof body?.error === 'string' ? body.error : `Request failed (${response.status})`;
}

// POST /api/render/{id}/session first, so <img> requests carry the sw_render
// cookie, then GET /api/render/{id}. Both send the token as X-Render-Token;
// neither goes through the signed-in API client, so a 401 never redirects.
async function loadDeck(id: string, token: string): Promise<Load> {
  const headers = { [TOKEN_HEADER]: token };
  const path = `/api/render/${encodeURIComponent(id)}`;
  const session = await fetch(`${path}/session`, { method: 'POST', headers });
  if (!session.ok) return { state: 'failed', error: await errorOf(session) };
  const response = await fetch(path, { headers });
  if (!response.ok) return { state: 'failed', error: await errorOf(response) };
  return { state: 'loaded', view: (await response.json()) as DeckPlayView };
}

// The image's id, the last segment of its /api/images/{id} path (L9).
function imageIdOf(image: HTMLImageElement): string {
  const src = image.getAttribute('src') ?? '';
  return new URL(src, window.location.href).pathname.split('/').pop() ?? src;
}

// One animation frame, so the fonts the slide uses have started loading
// before document.fonts.ready is read.
const nextFrame = () => new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

// Resolves once every image under root has loaded; rejects with the first
// image that fails.
function imagesLoaded(root: HTMLElement): Promise<void> {
  const images = Array.from(root.querySelectorAll('img'));
  return Promise.all(
    images.map(
      (image) =>
        new Promise<void>((resolve, reject) => {
          const failed = () => reject(new Error(`Rendering failed: image ${imageIdOf(image)}`));
          if (image.complete) {
            if (image.naturalWidth > 0) resolve();
            else failed();
            return;
          }
          image.addEventListener('load', () => resolve(), { once: true });
          image.addEventListener('error', failed, { once: true });
        }),
    ),
  ).then(() => undefined);
}

// /render/:id/:n: one slide at exactly 1920×1080 with every step revealed and
// no player UI, for an agent to screenshot (SW-2 L9, D21). Outside the sign-in
// gate and with no realtime subscription. The render token comes only from the
// fragment (#token=…), never the query string, and is removed from the address
// bar at once. data-ready="true" is set once fonts and images have loaded, or
// data-checks (the measured findings as JSON) is written with it. A failed load
// sets data-error with the reason, which is also drawn as text in the root so a
// screenshot of a failed page shows why.
export function RenderPage() {
  const { id = '', n } = useParams();
  const hash = useLocation().hash;
  const [token] = useState(() => new URLSearchParams(hash.slice(1)).get('token'));
  const [load, setLoad] = useState<Load>(() =>
    token ? { state: 'loading' } : { state: 'failed', error: TOKEN_MISSING },
  );
  const [ready, setReady] = useState<
    { ok: true; checks: SlideFinding[] } | { ok: false; error: string } | null
  >(null);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (window.location.hash) {
      window.history.replaceState(
        window.history.state,
        '',
        window.location.pathname + window.location.search,
      );
    }
  }, []);

  useEffect(() => {
    if (!token) return;
    let live = true;
    loadDeck(id, token).then(
      (result) => live && setLoad(result),
      () => live && setLoad({ state: 'failed', error: 'Network error' }),
    );
    return () => {
      live = false;
    };
  }, [id, token]);

  const view = load.state === 'loaded' ? load.view : null;
  const slide = view?.slides.find((s) => String(s.number) === n) ?? null;
  const slideDeck = useMemo<SlideDeck | null>(
    () =>
      view && {
        title: view.title,
        author: view.author,
        parts: view.parts,
        look: view.look,
        deepDives: deepDivesOf(view.slides),
      },
    [view],
  );

  useEffect(() => {
    const element = root.current;
    if (!slide || !element) return;
    let live = true;
    nextFrame()
      .then(() => fontsSettled(view?.look))
      .then(() => imagesLoaded(element))
      .then(() => {
        const frame = element.querySelector<HTMLElement>('[data-testid="slide-frame"]');
        if (!frame || !slide) throw new Error('Rendering failed: no slide frame');
        return measureSlide(frame, slide.number);
      })
      .then(
        (checks) => live && setReady({ ok: true, checks }),
        (error: Error) => live && setReady({ ok: false, error: error.message }),
      );
    return () => {
      live = false;
    };
  }, [slide, view]);

  const error =
    load.state === 'failed'
      ? load.error
      : view && !slide
        ? `No slide ${n ?? ''}`
        : ready && !ready.ok
          ? ready.error
          : undefined;

  return (
    <div
      ref={root}
      data-testid="render-slide"
      data-deck-id={id}
      data-slide={n}
      data-ready={ready?.ok && !error ? 'true' : undefined}
      data-checks={ready?.ok && !error ? JSON.stringify(ready.checks) : undefined}
      data-error={error}
      className="overflow-hidden"
      style={{ width: 1920, height: 1080 }}
    >
      {error !== undefined && (
        <p data-testid="render-error" className="m-0 p-16 text-5xl">
          {error}
        </p>
      )}
      {!error && slide && slideDeck && view && (
        <JoinProvider deckId={view.id} join={view.join} owner={false}>
          <SlideRenderer
            slide={slide}
            deck={slideDeck}
            total={view.slides.length}
            step={slide.steps}
          />
        </JoinProvider>
      )}
    </div>
  );
}
