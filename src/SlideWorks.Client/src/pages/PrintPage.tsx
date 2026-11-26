import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import type { DeckPrintView } from '../api/types';
import { deepDivesOf } from '../look/deepDivesOf';
import { fontsSettled } from '../look/fontsSettled';
import { OpenResponseScreenStatic } from '../openResponses/OpenResponseScreenLive';
import { QaScreenProvider, type QaScreenState } from '../qa/screen/QaScreenContext';
import { JoinProvider } from '../slides/JoinContext';
import { PollScreenProvider, type PollScreenSource } from '../slides/PollContext';
import { PrintingProvider } from '../slides/printing';
import { SlideRenderer } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import styles from './PrintPage.module.css';

// @page cannot be scoped by a CSS module, so the rule exists only while this page is mounted.
const PAGE_STYLE = <style>{'@page { size: 1920px 1080px; margin: 0; }'}</style>;

const TOKEN_HEADER = 'X-Render-Token';
const TOKEN_MISSING = 'Render token missing';

type Load =
  | { state: 'loading' }
  | { state: 'loaded'; view: DeckPrintView }
  | { state: 'failed'; error: string };

// The API's { error } text, or the status when the body has none.
async function errorOf(response: Response): Promise<string> {
  const body = (await response.json().catch(() => null)) as { error?: unknown } | null;
  return typeof body?.error === 'string' ? body.error : `Request failed (${response.status})`;
}

// POST /api/render/{id}/session first, so <img> requests carry the sw_render cookie, then GET /api/render/{id}/print.
// Both send the token as X-Render-Token; neither goes through the signed-in API client, so a 401 never redirects.
async function loadPrint(id: string, token: string): Promise<Load> {
  const headers = { [TOKEN_HEADER]: token };
  const path = `/api/render/${encodeURIComponent(id)}`;
  const session = await fetch(`${path}/session`, { method: 'POST', headers });
  if (!session.ok) return { state: 'failed', error: await errorOf(session) };
  const response = await fetch(`${path}/print`, { headers });
  if (!response.ok) return { state: 'failed', error: await errorOf(response) };
  return { state: 'loaded', view: (await response.json()) as DeckPrintView };
}

// The image's id, the last segment of its /api/images/{id} path.
function imageIdOf(image: HTMLImageElement): string {
  const src = image.getAttribute('src') ?? '';
  return new URL(src, window.location.href).pathname.split('/').pop() ?? src;
}

const nextFrame = () => new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

// Resolves once every image under root has loaded; rejects with the first image that fails.
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

const CLOUD_BODY = '[data-testid="word-cloud-body"]';

function slideOf(body: Element): string {
  return body.closest('[data-slide]')?.getAttribute('data-slide') ?? '?';
}

// The first word cloud whose words could not be packed: its packing threw, or its box has no size to pack into.
function brokenCloud(root: HTMLElement): Error | undefined {
  for (const body of root.querySelectorAll(`${CLOUD_BODY}:not([data-packed="true"])`)) {
    const box = body.firstElementChild as HTMLElement | null;
    if (box !== null && (box.offsetWidth === 0 || box.offsetHeight === 0))
      return new Error(`Rendering failed: word cloud on slide ${slideOf(body)}`);
  }
  const failed = root.querySelector(`${CLOUD_BODY}[data-pack-error="true"]`);
  return failed === null
    ? undefined
    : new Error(`Rendering failed: word cloud on slide ${slideOf(failed)}`);
}

// Resolves once every word-cloud body under root carries data-packed="true"; rejects when a cloud cannot pack, so a
// render never waits out its deadline.
function cloudsPacked(root: HTMLElement): Promise<void> {
  const pending = () => root.querySelectorAll(`${CLOUD_BODY}:not([data-packed="true"])`).length;
  return new Promise((resolve, reject) => {
    const check = () => {
      const broken = root.querySelector(`${CLOUD_BODY}[data-pack-error="true"]`);
      if (broken !== null) {
        observer.disconnect();
        return reject(brokenCloud(root));
      }
      if (pending() === 0) {
        observer.disconnect();
        return resolve();
      }
      return undefined;
    };
    const observer = new MutationObserver(() => {
      check();
    });
    observer.observe(root, { subtree: true, childList: true, attributes: true });
    check();
    // Once the boxes have been laid out, a cloud with words and no room will never pack.
    void nextFrame().then(() => {
      const broken = brokenCloud(root);
      if (broken !== undefined) {
        observer.disconnect();
        reject(broken);
      }
    });
  });
}

// The poll entry of a slide as the provider's source, or null for a slide with no entry.
function pollSourceOf(view: DeckPrintView, number: number): PollScreenSource {
  const entry = view.polls.find((poll) => poll.slide === number);
  if (entry === undefined) return null;
  const { slide, ...screen } = entry;
  return { slide, screen };
}

// /print/:id: every slide of the deck stacked, 1920×1080 each with every step revealed, for the PDF engine. Outside the
// sign-in gate, with no realtime subscription. The render token comes only from the fragment (#token=…) and is removed
// from the address bar at once. data-ready="true" is set once fonts, images and word clouds have settled; a failure sets
// data-error with the reason, also drawn as text.
export function PrintPage() {
  const { id = '' } = useParams();
  const hash = useLocation().hash;
  const [token] = useState(() => new URLSearchParams(hash.slice(1)).get('token'));
  const [load, setLoad] = useState<Load>(() =>
    token ? { state: 'loading' } : { state: 'failed', error: TOKEN_MISSING },
  );
  const [ready, setReady] = useState<{ ok: true } | { ok: false; error: string } | null>(null);
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
    loadPrint(id, token).then(
      (result) => live && setLoad(result),
      () => live && setLoad({ state: 'failed', error: 'Network error' }),
    );
    return () => {
      live = false;
    };
  }, [id, token]);

  const view = load.state === 'loaded' ? load.view : null;
  const deck = view?.deck ?? null;
  const slideDeck = useMemo<SlideDeck | null>(
    () =>
      deck && {
        title: deck.title,
        author: deck.author,
        parts: deck.parts,
        look: deck.look,
        deepDives: deepDivesOf(deck.slides),
      },
    [deck],
  );

  const openEvents = useMemo(() => (view ? [...view.wordClouds, ...view.openTexts] : []), [view]);
  const qaState = useMemo<QaScreenState | undefined>(
    () =>
      view?.qa
        ? { kind: 'on', screen: { ...view.qa, onScreen: null, showOn: 'qaSlide' } }
        : undefined,
    [view],
  );

  useEffect(() => {
    const element = root.current;
    if (!deck || !element) return;
    let live = true;
    nextFrame()
      .then(() => fontsSettled(deck.look))
      .then(() => imagesLoaded(element))
      .then(() => cloudsPacked(element))
      .then(
        () => live && setReady({ ok: true }),
        (error: Error) => live && setReady({ ok: false, error: error.message }),
      );
    return () => {
      live = false;
    };
  }, [deck]);

  const error = load.state === 'failed' ? load.error : ready && !ready.ok ? ready.error : undefined;

  return (
    <div
      ref={root}
      data-testid="print-deck"
      data-deck-id={id}
      data-ready={ready?.ok && !error ? 'true' : undefined}
      data-error={error}
    >
      {error !== undefined && (
        <p data-testid="print-error" className="m-0 p-16 text-5xl">
          {error}
        </p>
      )}
      {PAGE_STYLE}
      {!error && view && deck && slideDeck && (
        <PrintingProvider>
          <JoinProvider deckId={deck.id} join={deck.join} owner={false}>
            <OpenResponseScreenStatic events={openEvents}>
              <QaScreenProvider state={qaState}>
                {deck.slides.map((slide) => (
                  <section
                    key={slide.number}
                    data-testid="print-slide"
                    data-slide={slide.number}
                    className={styles.slide}
                  >
                    <PollScreenProvider source={pollSourceOf(view, slide.number)}>
                      <SlideRenderer
                        slide={slide}
                        deck={slideDeck}
                        total={deck.slides.length}
                        step={slide.steps}
                      />
                    </PollScreenProvider>
                  </section>
                ))}
              </QaScreenProvider>
            </OpenResponseScreenStatic>
          </JoinProvider>
        </PrintingProvider>
      )}
    </div>
  );
}
