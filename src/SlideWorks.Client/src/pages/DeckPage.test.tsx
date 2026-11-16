import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { DeckPageView, SharingView } from '../api/types';
import { deckPage, sharing } from '../test-support/fixtures/screens';
import { DeckPage } from './DeckPage';

// The sharing dialog is still a shell that never saves (SW-46 builds it), so
// this stands in for it: once open, it offers a button that reports a save,
// as the real one does after its PUT.
vi.mock('../components/SharingDialog', () => ({
  SharingDialog: ({ open, onSaved }: { open: boolean; onSaved(view: SharingView): void }) =>
    open ? (
      <button type="button" data-testid="fake-sharing-save" onClick={() => onSaved(sharing)}>
        Save
      </button>
    ) : null,
}));

// No hub in jsdom; DeckChanged re-reads are covered by DeckPage.spec.tsx.
vi.mock('../realtime/useDeckRealtime', () => ({ useDeckRealtime: () => {} }));

const deckPath = `/api/decks/${deckPage.id}`;

let host: HTMLDivElement;
let root: Root;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  host = document.createElement('div');
  document.body.append(host);
  root = createRoot(host);
});

afterEach(() => {
  act(() => root.unmount());
  host.remove();
  vi.unstubAllGlobals();
});

const byTestId = (id: string) => host.querySelector(`[data-testid="${id}"]`);

async function until(check: () => boolean) {
  await vi.waitFor(() => expect(check()).toBe(true));
}

it('deck page re-reads after a sharing save', async () => {
  // Slideless views, so no thumbnail needs layout in jsdom.
  const before: DeckPageView = { ...deckPage, slides: [] };
  const after: DeckPageView = { ...before, sharing: { level: 'named', viewerCount: 2 } };
  const reads: string[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      reads.push(String(input));
      return new Response(JSON.stringify(reads.length === 1 ? before : after), { status: 200 });
    }),
  );

  await act(async () =>
    root.render(
      <MemoryRouter initialEntries={[`/decks/${deckPage.id}`]}>
        <Routes>
          <Route path="/decks/:id" element={<DeckPage />} />
        </Routes>
      </MemoryRouter>,
    ),
  );
  await until(() => byTestId('deck-sharing-level')?.textContent === 'Anyone with the link');

  await act(async () => (byTestId('deck-share') as HTMLButtonElement).click());
  await act(async () => (byTestId('fake-sharing-save') as HTMLButtonElement).click());

  await until(() => byTestId('deck-sharing-level')?.textContent === '2 named viewers');
  expect(reads).toEqual([deckPath, deckPath]);
});
