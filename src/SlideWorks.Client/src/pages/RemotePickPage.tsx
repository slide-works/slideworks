import { useCallback, useEffect, useRef, useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import { getLivePresentations } from '../api/presenter';
import { api } from '../api/client';
import type { LivePresentation, MeView } from '../api/types';
import { LiveCard } from '../remote/LiveCard';

type Read =
  { kind: 'loading' } | { kind: 'failed' } | { kind: 'loaded'; talks: LivePresentation[] };

// /remote, where a phone picks which of the person's live talks to drive (P8): signed in, with no Shell chrome.
// The QR code's link (/remote?deck={id}) lands here too: a deck that is live goes straight to its speaker mode,
// and one that isn't shows the list with a note. The list is read once, then again whenever the page becomes visible
// (a phone coming back from the lock screen), with no polling.
export function RemotePickPage() {
  const [params] = useSearchParams();
  const wanted = params.get('deck');
  const [read, setRead] = useState<Read>({ kind: 'loading' });
  const [name, setName] = useState<string | null>(null);
  const latest = useRef(0);
  const mounted = useRef(true);

  const load = useCallback(() => {
    const mine = ++latest.current;
    getLivePresentations().then(
      (view) =>
        mounted.current &&
        mine === latest.current &&
        setRead({ kind: 'loaded', talks: view.presentations }),
      () => mounted.current && mine === latest.current && setRead({ kind: 'failed' }),
    );
  }, []);

  useEffect(() => {
    mounted.current = true;
    load();
    const onVisible = () => {
      if (document.visibilityState === 'visible') load();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      mounted.current = false;
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [load]);

  // The gate has already checked the session; this only learns the name to show. A failure leaves the line out.
  useEffect(() => {
    let active = true;
    api.get<MeView>('/api/me').then(
      (me) => active && setName(me.name),
      () => undefined,
    );
    return () => {
      active = false;
    };
  }, []);

  if (wanted && read.kind === 'loaded' && read.talks.some((talk) => talk.deckId === wanted)) {
    return <Navigate to={`/remote/${encodeURIComponent(wanted)}`} replace />;
  }
  const notLive = wanted && read.kind === 'loaded';

  return (
    <main
      data-testid="remote-pick"
      className="mx-auto flex min-h-screen max-w-md flex-col gap-3 bg-[#0a0e16] px-4 py-8 text-[#e2e8f0]"
    >
      {name && (
        <p data-testid="remote-signed-in" className="text-xs text-[#94a3b8]">
          Signed in as {name}
        </p>
      )}
      <h1 className="text-[17px] font-bold">Presenting now</h1>
      {notLive && (
        <p data-testid="remote-not-live" role="status" className="text-xs text-[#fbbf24]">
          That talk isn't live right now.
        </p>
      )}
      {read.kind === 'failed' && (
        <p data-testid="remote-error" role="alert" className="text-sm text-[#fca5a5]">
          Couldn't load your talks. Check your connection and come back to this screen to try again.
        </p>
      )}
      {read.kind === 'loaded' && read.talks.length > 0 && (
        <ul className="grid gap-3">
          {read.talks.map((talk) => (
            <LiveCard key={talk.deckId} talk={talk} />
          ))}
        </ul>
      )}
      {read.kind === 'loaded' && read.talks.length === 0 && (
        <p
          data-testid="remote-empty"
          className="rounded-xl border border-[#1f2937] bg-[#111827] p-3 text-xs text-[#94a3b8]"
        >
          Not presenting anything. Open a deck's presenter view on your laptop and it shows up here.
        </p>
      )}
    </main>
  );
}
