import { Link } from 'react-router-dom';
import type { DeckPageView } from '../../api/types';
import { LaterButton } from './LaterButton';
import { sharingLabel } from './sharingLabel';

type DeckHeaderProps = { view: DeckPageView; onShare(): void };

// The deck's title row: slide count, who can open it, the controls later
// Features bring, Share and Present.
export function DeckHeader({ view, onShare }: DeckHeaderProps) {
  const count = view.slides.length;
  return (
    <div className="flex flex-wrap items-center gap-3">
      <h2 data-testid="deck-title" className="text-xl font-bold tracking-tight text-ink">
        {view.title}
      </h2>
      <span
        data-testid="deck-slide-count"
        className="rounded-full bg-panel-2 px-2 py-0.5 text-xs text-ink-muted"
      >
        {count} {count === 1 ? 'slide' : 'slides'}
      </span>
      <span
        data-testid="deck-sharing-level"
        className="rounded-full bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent-dark"
      >
        {sharingLabel(view.sharing)}
      </span>
      <div className="flex flex-wrap items-center gap-2 sm:ml-auto">
        <LaterButton label="Check slides" feature="SW-3" />
        <LaterButton label="Presenter view" feature="SW-4" />
        {view.isOwner && (
          <button type="button" data-testid="deck-share" className="btn-ghost" onClick={onShare}>
            Share
          </button>
        )}
        <Link to={view.playUrl} data-testid="deck-present" className="btn-primary no-underline">
          ▶ Present
        </Link>
      </div>
    </div>
  );
}
