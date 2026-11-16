import { Link, Outlet } from 'react-router-dom';
import type { MeView } from './api/types';

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0].toUpperCase())
    .join('');
}

// The layout every signed-in page renders inside: the top bar, then the routed page.
export function Shell({ me }: { me: MeView }) {
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
          </nav>
          <div data-testid="user-menu" className="ml-auto">
            <span
              title={me.name}
              className="grid h-7 w-7 place-items-center rounded-full bg-accent-soft text-[11px] font-semibold text-accent-dark"
            >
              {initials(me.name)}
            </span>
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-6xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </div>
  );
}
