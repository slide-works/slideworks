import type { DeckPageLook, DeckPageView } from '../api/types';
import { LogoField } from './panel/LogoField';

export type LookPanelProps = { deck: DeckPageView; onSaved(look: DeckPageLook): void };

// TODO: a shell with inert fields; the Look panel (template, palette, chrome
// switches, draft, logo, saved with PUT /api/decks/{id}/look) replaces it.
export function LookPanel({ deck }: LookPanelProps) {
  const { look } = deck;
  return (
    <section data-testid="look-panel" aria-label="Look" className="panel grid gap-3 p-4">
      <h2 className="text-base font-semibold text-ink">Look</h2>
      <label className="grid gap-1 text-meta">
        Template
        <select data-testid="look-template" value={look.template.name} disabled>
          <option value={look.template.name}>{look.template.label}</option>
        </select>
      </label>
      <label className="grid gap-1 text-meta">
        Palette
        <select data-testid="look-palette" value={look.palette.name} disabled>
          <option value={look.palette.name}>{look.palette.label}</option>
        </select>
      </label>
      <label className="text-meta">
        <input
          data-testid="look-tracker"
          type="checkbox"
          checked={look.template.chrome.tracker}
          disabled
        />{' '}
        Part tracker
      </label>
      <label className="text-meta">
        <input
          data-testid="look-progress"
          type="checkbox"
          checked={look.template.chrome.progress}
          disabled
        />{' '}
        Progress bar
      </label>
      <label className="grid gap-1 text-meta">
        Footer
        <select data-testid="look-footer" value={look.template.chrome.footer} disabled>
          <option value="author">Author</option>
          <option value="date">Date</option>
          <option value="off">Off</option>
        </select>
      </label>
      <label className="text-meta">
        <input data-testid="look-draft" type="checkbox" checked={look.draft} disabled /> Draft
      </label>
      <LogoField deckId={deck.id} logo={look.logo} onChange={() => {}} />
    </section>
  );
}
