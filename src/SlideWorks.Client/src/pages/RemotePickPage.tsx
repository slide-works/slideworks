import { useCallback, useEffect, useRef, useState } from 'react';
import { Navigate, useOutletContext, useSearchParams } from 'react-router-dom';
import { getLivePresentations } from '../api/presenter';
import type { LivePresentation, MeView } from '../api/types';
import { LiveCard } from '../remote/LiveCard';

// talks is null until the first read succeeds; a failed re-read keeps the last list and sets failed.
interface Read {
  talks: LivePresentation[] | null;
  failed: boolean;
}

// /remote, where a phone picks which of the person's live talks to drive (P8): signed in, with no Shell chrome.
// The QR code's link (/remote?deck={id}) lands here too: a deck that is live goes straight to its speaker mode,
// and one that isn't shows the list with a note. The list is read once, then again whenever the page becomes visible
// (a phone coming back from the lock screen), with no polling.
export function RemotePickPage() {
  // The signed-in person, from the gate's Outlet context: the page makes no /api/me call of its own.
  const me = useOutletContext<MeView>();
  const [params] = useSearchParams();
  const wanted = params.get('deck');
  const [read, setRead] = useState<Read>({ talks: null, failed: false });
  const heading = useRef<HTMLHeadingElement>(null);
  const refocus = useRef(false);
  const latest = useRef(0);
  const mounted = useRef(true);

  const load = useCallback(() => {
    const mine = ++latest.current;
    getLivePresentations().then(
      (view) => {
        if (!mounted.current || mine !== latest.current) return;
        setRead({ talks: view.presentations, failed: false });
        if (refocus.current) {
          refocus.current = false;
          // The Try again button is gone now; put focus on the heading so it isn't lost.
          requestAnimationFrame(() => heading.current?.focus());
        }
      },
      () =>
        mounted.current &&
        mine === latest.current &&
        setRead((last) => ({ talks: last.talks, failed: true })),
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

  const tryAgain = () => {
    refocus.current = true;
    load();
  };

  if (wanted && read.talks?.some((talk) => talk.deckId === wanted)) {
    return <Navigate to={`/remote/${encodeURIComponent(wanted)}`} replace />;
  }
  const notLive = wanted && read.talks !== null;

  return (
    <main
      data-testid="remote-pick"
      className="mx-auto flex min-h-screen max-w-md flex-col gap-3 bg-[#0a0e16] px-4 py-8 text-[#e2e8f0]"
    >
      <h1 ref={heading} tabIndex={-1} className="text-[17px] font-bold outline-none">
        Presenting now
      </h1>
      <p data-testid="remote-signed-in" className="text-xs text-[#94a3b8]">
        Signed in as {me.name}
      </p>
      {notLive && (
        <p data-testid="remote-not-live" role="status" className="text-xs text-[#fbbf24]">
          That talk isn't live right now.
        </p>
      )}
      {read.failed && (
        <div className="flex flex-col items-start gap-2">
          <p data-testid="remote-error" role="alert" className="text-sm text-[#fca5a5]">
            Couldn't load your talks. Check your connection and try again.
          </p>
          <button
            type="button"
            onClick={tryAgain}
            className="rounded-lg border border-[#334155] px-3 py-2 text-sm font-semibold"
          >
            Try again
          </button>
        </div>
      )}
      {read.talks && read.talks.length > 0 && (
        <ul className="grid gap-3">
          {read.talks.map((talk) => (
            <LiveCard key={talk.deckId} talk={talk} />
          ))}
        </ul>
      )}
      {read.talks && read.talks.length === 0 && (
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
