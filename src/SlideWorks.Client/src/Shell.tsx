import { useEffect, useRef } from 'react';
import { Link, Outlet } from 'react-router-dom';
import { api } from './api/client';
import type { MeView } from './api/types';
import { AdminLink } from './components/AdminLink';
import { UserMenu } from './components/UserMenu';

// The layout every signed-in page renders inside: the top bar, then the routed page.
export function Shell({ me }: { me: MeView }) {
  const sentBrowserZone = useRef(false);
  // A person with no time zone saved gets the browser's, once; it only fills an empty zone (fromBrowser), so a zone
  // picked on another device is never overwritten. A failed fill is silent: the zone stays empty and dates read UTC.
  useEffect(() => {
    if (me.access !== 'approved' || me.timeZone !== null || sentBrowserZone.current) return;
    sentBrowserZone.current = true;
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    api.put('/api/me/time-zone', { timeZone, fromBrowser: true }).catch(() => undefined);
  }, [me.access, me.timeZone]);

  return (
    <div data-testid="app-shell">
      <header className="sticky top-0 z-10 border-b border-edge bg-panel">
        <div className="mx-auto flex w-full max-w-6xl items-center gap-4 px-4 py-3 sm:px-6">
          <Link to="/decks" className="flex items-center gap-2 text-ink no-underline">
            <span aria-hidden className="h-4 w-5 rounded-sm bg-accent" />
            <span className="text-sm font-bold tracking-tight">SlideWorks</span>
          </Link>
          <nav className="flex gap-1">
            <Link
              to="/decks"
              className="rounded-md px-2.5 py-1 text-meta font-medium text-ink-muted no-underline hover:bg-panel-2 hover:text-ink"
            >
              Decks
            </Link>
            <Link
              to="/templates"
              className="rounded-md px-2.5 py-1 text-meta font-medium text-ink-muted no-underline hover:bg-panel-2 hover:text-ink"
            >
              Templates
            </Link>
          </nav>
          <AdminLink me={me} />
          <div data-testid="user-menu" className="ml-auto">
            <UserMenu me={me} />
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-6xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </div>
  );
}
