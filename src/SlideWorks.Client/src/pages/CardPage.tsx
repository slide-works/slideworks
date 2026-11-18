import { useEffect, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import type { LookArt } from '../api/look';
import type { CardView } from '../api/types';
import { fontsSettled } from '../look/fontsSettled';
import { lookStyle } from '../look/lookStyle';
import { WAVE_PATHS } from '../slides/art/waves';
import styles from './CardPage.module.css';

// /card/:id and /card/generic: the 1200×630 share card the server screenshots
// (SW-2 L9, D12; mockups section 09). Outside the sign-in gate and with no
// realtime subscription. A deck's card needs the render token, which comes only
// from the fragment (#token=…) and goes only in the X-Render-Token header, on
// every request the page makes: card.json, then the logo, fetched with the
// header and shown from a blob URL. It sends no cookie, so no signed-in request
// can slip in. The generic card says only "SlideWorks" and makes no request.
//
// The logo isn't loaded through POST /api/render/{id}/session and the sw_render
// cookie: the server's engine opens this page at its loopback address, and the
// cross-site write guard refuses a POST whose Origin isn't the public URL.
//
// The token is read once and removed from the address bar, as RenderPage does.
export function CardPage() {
  const { id } = useParams();
  const hash = useLocation().hash;
  const [token] = useState(() => new URLSearchParams(hash.slice(1)).get('token'));
  useEffect(() => {
    if (window.location.hash) {
      window.history.replaceState(
        window.history.state,
        '',
        window.location.pathname + window.location.search,
      );
    }
  }, []);
  if (id === undefined) return <ShareCard deckId={undefined} card={undefined} error={undefined} />;
  return <DeckCard key={`${id}#${token ?? ''}`} deckId={id} token={token} />;
}

const TOKEN_HEADER = 'X-Render-Token';
const TOKEN_MISSING = 'Render token missing';

type Loaded = { card: CardView; logo: string | undefined } | { error: string };

function DeckCard({ deckId, token }: { deckId: string; token: string | null }) {
  const [loaded, setLoaded] = useState<Loaded | undefined>(
    token ? undefined : { error: TOKEN_MISSING },
  );
  useEffect(() => {
    if (!token) return;
    let live = true;
    let logo: string | undefined;
    readCard(deckId, token).then(
      (read) => {
        logo = read.logo;
        if (live) setLoaded(read);
        else if (logo) URL.revokeObjectURL(logo);
      },
      (error: unknown) =>
        live && setLoaded({ error: error instanceof Error ? error.message : String(error) }),
    );
    return () => {
      live = false;
      if (logo) URL.revokeObjectURL(logo);
    };
  }, [deckId, token]);
  return (
    <ShareCard
      deckId={deckId}
      card={loaded && 'card' in loaded ? loaded.card : undefined}
      logo={loaded && 'card' in loaded ? loaded.logo : undefined}
      error={loaded && 'error' in loaded ? loaded.error : undefined}
      loading={loaded === undefined}
    />
  );
}

// card.json, then the logo when a link deck has one (a generic card shows none).
async function readCard(
  deckId: string,
  token: string,
): Promise<{ card: CardView; logo: string | undefined }> {
  const response = await get(`/api/render/${encodeURIComponent(deckId)}/card`, token);
  if (!response.ok) throw new Error(`Card request failed: ${await errorOf(response)}`);
  const card = (await response.json()) as CardView;
  const logoUrl = card.generic ? undefined : card.look.logo?.url;
  if (!logoUrl) return { card, logo: undefined };
  const image = await get(logoUrl, token);
  if (!image.ok) throw new Error(imageFailed(logoUrl));
  return { card, logo: URL.createObjectURL(await image.blob()) };
}

// The data-error text for a logo that fails, 'image <id>' with the id from its
// /api/images/{id} path. The engine reports it as 'Rendering failed: image <id>'
// (L9), so the page doesn't add the prefix itself.
const imageFailed = (url: string) => `image ${url.split('/').pop() ?? url}`;

const get = (path: string, token: string) =>
  fetch(path, { credentials: 'omit', headers: { [TOKEN_HEADER]: token } });

async function errorOf(response: Response): Promise<string> {
  try {
    const parsed = (await response.json()) as { error?: string };
    return parsed.error ?? `${response.status}`;
  } catch {
    return `${response.status}`;
  }
}

type ShareCardProps = {
  deckId: string | undefined;
  card: CardView | undefined;
  logo?: string;
  error: string | undefined;
  loading?: boolean;
};

// The card itself. data-ready is set once its fonts and logo have loaded, and
// data-error carries the first failure, so a screenshot never waits forever.
function ShareCard({ deckId, card, logo, error, loading = false }: ShareCardProps) {
  const generic = deckId === undefined || card?.generic === true;
  const [logoState, setLogoState] = useState<'pending' | 'loaded' | 'failed'>('pending');
  const [fontsReady, setFontsReady] = useState(false);
  const shown = deckId === undefined || card !== undefined;

  useEffect(() => {
    if (!shown) return;
    let live = true;
    // One frame first, so the card's fonts have started loading before
    // document.fonts.ready is read.
    const frame = requestAnimationFrame(() => {
      void fontsSettled(card?.look).then(() => live && setFontsReady(true));
    });
    return () => {
      live = false;
      cancelAnimationFrame(frame);
    };
  }, [shown, card]);

  const failure =
    error ??
    (logoState === 'failed' && card?.look.logo ? imageFailed(card.look.logo.url) : undefined);
  const ready =
    !loading && failure === undefined && shown && fontsReady && (!logo || logoState === 'loaded');

  return (
    <div
      data-testid="share-card"
      data-deck-id={deckId}
      data-generic={generic ? 'true' : 'false'}
      data-ready={ready ? 'true' : undefined}
      data-error={failure}
      className={styles.card}
      style={card && !card.generic ? lookStyle(card.look) : undefined}
    >
      {shown && (card === undefined || card.generic) && <Waves />}
      {shown && card !== undefined && !card.generic && <CardArt art={card.look.art} />}
      {shown && generic && <h1 className={styles.title}>SlideWorks</h1>}
      {shown && !generic && card && (
        <>
          <div className={styles.kicker}>SlideWorks</div>
          <h1 className={styles.title} data-testid="share-card-title">
            {card.title}
          </h1>
          {card.subtitle && (
            <p className={styles.subtitle} data-testid="share-card-subtitle">
              {card.subtitle}
            </p>
          )}
          <div className={styles.footer}>
            <span data-testid="share-card-byline">
              <b>{slidesLabel(card.slideCount)}</b>
              {card.author && ` · ${card.author}`}
            </span>
            {logo ? (
              <img
                className={styles.logo}
                src={logo}
                alt=""
                onLoad={() => setLogoState('loaded')}
                onError={() => setLogoState('failed')}
              />
            ) : (
              <div className={styles.swatch} data-testid="share-card-swatch" aria-hidden="true">
                <i />
                <i />
                <i />
              </div>
            )}
            <span>{card.look.palette.label}</span>
          </div>
        </>
      )}
    </div>
  );
}

function slidesLabel(count: number): string {
  return count === 1 ? '1 slide' : `${count} slides`;
}

// A deck card's art from its look: the waves generator in accent-2, an uploaded
// image, or nothing. A generator it does not know draws nothing.
function CardArt({ art }: { art: LookArt }) {
  if (art.kind === 'generator' && art.generator === 'waves') return <Waves tone="deck" />;
  if (art.kind === 'image' && art.url !== null) {
    return <img className={`${styles.art} ${styles.artImage}`} src={art.url} alt="" />;
  }
  return null;
}

// The waves title art (mockups section 09), stroked with the palette's glow, or
// with accent-2 on a deck card.
function Waves({ tone = 'glow' }: { tone?: 'glow' | 'deck' }) {
  return (
    <svg
      className={tone === 'deck' ? `${styles.art} ${styles.artDeck}` : styles.art}
      viewBox="0 0 1920 1080"
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      {WAVE_PATHS.map((path) => (
        <path key={path.d} d={path.d} style={{ opacity: path.opacity }} />
      ))}
    </svg>
  );
}
