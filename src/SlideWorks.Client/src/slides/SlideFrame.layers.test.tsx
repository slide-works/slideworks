import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { LookView } from '../api/types';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { SlideFrame } from './SlideFrame';

// Each look layer is replaced by a marker that says which layer it is, where it was placed, and which look
// useSlideLook() gave it.
const { marker, looks } = vi.hoisted(() => {
  const looks = new Map<string, unknown>();
  const marker = async (name: string) => {
    const { createElement } = await import('react');
    const { useSlideLook } = await import('./chrome/lookContext');
    return function Marker({ placement }: { placement?: string }) {
      const key = placement === undefined ? name : `${name}:${placement}`;
      looks.set(key, useSlideLook());
      return createElement('i', { 'data-layer': key });
    };
  };
  return { marker, looks };
});

vi.mock('./chrome/layers/FontFaces', async () => ({ FontFaces: await marker('FontFaces') }));
vi.mock('./chrome/layers/TitleArt', async () => ({ TitleArt: await marker('TitleArt') }));
vi.mock('./chrome/layers/Ornament', async () => ({ Ornament: await marker('Ornament') }));
vi.mock('./chrome/layers/TopBar', async () => ({ TopBar: await marker('TopBar') }));
vi.mock('./chrome/layers/TemplateLogo', async () => ({
  TemplateLogo: await marker('TemplateLogo'),
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const look: LookView = { ...lookViewOf('talk', 'cobalt'), logo: { url: '/api/images/logo-1' } };

let root: Root;
let host: HTMLDivElement;

async function frame(layout: 'bullets' | 'title') {
  await act(async () =>
    root.render(
      <SlideFrame
        layout={layout}
        surface={layout === 'title' ? 'dark' : 'light'}
        part={null}
        parts={['The cost', 'Slice it']}
        number={3}
        total={16}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={look}
        deepDive={false}
        titleStamp={null}
      >
        <h2>Every slip we had last year started as a big batch</h2>
      </SlideFrame>,
    ),
  );
  return host.querySelector('.sw-frame')!;
}

const layersIn = (element: Element) =>
  [...element.children].map((child) => child.getAttribute('data-layer'));

beforeEach(() => {
  looks.clear();
  host = document.createElement('div');
  root = createRoot(host);
});

afterEach(async () => {
  await act(async () => root.unmount());
});

describe('SlideFrame', () => {
  it('frame mounts the look layers', async () => {
    const slide = await frame('bullets');

    const layers = slide.querySelector("[data-testid='slide-layers']")!;
    expect(slide.firstElementChild).toBe(layers);
    expect(layers.className).toBe('sw-layers');
    expect(layersIn(layers)).toEqual([
      'FontFaces',
      'TitleArt',
      'Ornament',
      'TopBar',
      'TemplateLogo:frame',
    ]);

    const footer = slide.querySelector('.sw-footer')!;
    const end = footer.querySelector('.sw-footer-end')!;
    expect(footer.lastElementChild).toBe(end);
    const logo = end.querySelector("[data-testid='slide-logo']")!;
    const templateLogo = end.querySelector("[data-layer='TemplateLogo:footer']")!;
    expect(end.lastElementChild).toBe(templateLogo);
    expect(templateLogo.previousElementSibling).toBe(logo);

    expect([...looks.keys()].sort()).toEqual([
      'FontFaces',
      'Ornament',
      'TemplateLogo:footer',
      'TemplateLogo:frame',
      'TitleArt',
      'TopBar',
    ]);
    for (const seen of looks.values()) expect(seen).toBe(look);
  });

  it('a slide without a footer still mounts the layers, with the frame logo only', async () => {
    const slide = await frame('title');

    const layers = slide.querySelector("[data-testid='slide-layers']")!;
    expect(slide.firstElementChild).toBe(layers);
    expect(layersIn(layers)).toEqual([
      'FontFaces',
      'TitleArt',
      'Ornament',
      'TopBar',
      'TemplateLogo:frame',
    ]);
    expect(slide.querySelector("[data-layer='TemplateLogo:footer']")).toBeNull();
  });
});
