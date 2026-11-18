import { test, expect } from '@playwright/experimental-ct-react';
import type { LookView } from '../../../api/types';
import { lookStyle } from '../../../look/lookStyle';
import { lookViewOf } from '../../../test-support/fixtures/lookViewOf';
import { SlideFrame } from '../../SlideFrame';
import { TitleLayout } from '../../layouts/TitleLayout';

test.use({ viewport: { width: 1920, height: 1080 } });

function look(colours: string[]) {
  return lookViewOf('talk', 'cobalt', {
    ornament: { colours: colours as LookView['ornament']['colours'] },
  });
}

// A plain function, not a component: CT can only mount components from source files.
function slide(view: LookView) {
  return (
    <div style={lookStyle(view)}>
      <SlideFrame
        layout="title"
        surface="dark"
        part={null}
        number={1}
        total={16}
        parts={[]}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={view}
        deepDive={false}
        titleStamp={null}
      >
        <TitleLayout
          slots={{ title: 'Ship smaller', kicker: 'Q4 review' }}
          step={0}
          deck={{ title: 'Ship smaller', author: null, parts: [], look: view, deepDives: [] }}
        />
      </SlideFrame>
    </div>
  );
}

// The dot elements of the title slide, the first child of the layout's stack.
const DOTS = '[data-testid="layout-title"] > div[aria-hidden="true"]';

async function colours(page: import('playwright/test').Page) {
  return page.locator(`${DOTS} i`).evaluateAll((dots) =>
    dots.map((dot) => {
      const style = getComputedStyle(dot);
      return { background: style.backgroundColor, opacity: style.opacity };
    }),
  );
}

// What a token resolves to as a background colour, read off the frame.
async function resolved(page: import('playwright/test').Page, token: string) {
  return page.locator(DOTS).evaluate((el, name) => {
    const probe = document.createElement('i');
    probe.style.background = `var(--sw-${name})`;
    el.appendChild(probe);
    const colour = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return colour;
  }, token);
}

test("draws today's three dots for a built-in look", async ({ mount, page }) => {
  await mount(slide(look([])));

  const dots = await colours(page);
  expect(dots.map((dot) => dot.opacity)).toEqual(['0.9', '0.6', '0.35']);
  const glow = await resolved(page, 'accent-glow');
  expect(dots.every((dot) => dot.background === glow)).toBe(true);
  await expect(page.getByTestId('title-ornament')).toHaveCount(0);
});

test('draws one dot per colour', async ({ mount, page }) => {
  await mount(slide(look(['accent', 'good', 'warn', 'bad'])));

  await expect(page.getByTestId('title-ornament')).toHaveAttribute('aria-hidden', 'true');
  const dots = await colours(page);
  expect(dots).toHaveLength(4);
  expect(dots.map((dot) => dot.opacity)).toEqual(['1', '1', '1', '1']);
  for (const [index, token] of ['accent', 'good', 'warn', 'bad'].entries()) {
    expect(dots[index].background).toBe(await resolved(page, token));
  }
  const boxes = await page.locator(`${DOTS} i`).evaluateAll((els) =>
    els.map((el) => {
      const { x, width, height } = el.getBoundingClientRect();
      return { x, width, height };
    }),
  );
  expect(boxes.every((box) => box.width === 18 && box.height === 18)).toBe(true);
  expect(boxes[1].x - boxes[0].x).toBe(32);
});

test('uses an extra colour', async ({ mount, page }) => {
  await mount(slide(look(['extra-1', 'accent-2'])));

  const dots = await colours(page);
  expect(dots).toHaveLength(2);
  expect(dots[0].background).toBe(await resolved(page, 'extra-1'));
  expect(dots[1].background).toBe(await resolved(page, 'accent-2'));
});

test('skips an unknown token name', async ({ mount, page }) => {
  await mount(slide(look(['accent', 'red; display: none', 'night'])));

  const dots = await colours(page);
  expect(dots).toHaveLength(1);
  expect(dots[0].background).toBe(await resolved(page, 'accent'));
});

test('stops at six dots', async ({ mount, page }) => {
  await mount(
    slide(look(['accent', 'accent-2', 'accent-glow', 'good', 'warn', 'bad', 'extra-1', 'extra-2'])),
  );

  await expect(page.locator(`${DOTS} i`)).toHaveCount(6);
});

// Baselines come from CI's playwright job (ct-new-baselines), never this host.
test('screenshot: title slide with four ornament dots', async ({ mount, page }) => {
  await mount(slide(look(['accent', 'accent-2', 'good', 'warn'])));
  await expect(page.locator(`${DOTS} i`)).toHaveCount(4);
  await expect(page).toHaveScreenshot('title-ornament-four.png');
});
