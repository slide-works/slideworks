import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { LookView } from '../../../api/types';
import { lookStyle } from '../../../look/lookStyle';
import { lookViewOf } from '../../../test-support/fixtures/lookViewOf';
import { SlideFrame, type SlideFrameProps } from '../../SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

function look(topBar: LookView['topBar']) {
  return lookViewOf('talk', 'cobalt', { topBar });
}

// A plain function, not a component: CT can only mount components from source files.
function slide(
  view: LookView,
  props: Partial<Omit<SlideFrameProps, 'children'>> = {},
  extra: Record<string, string> = {},
) {
  return (
    <div style={{ ...lookStyle(view), ...extra }}>
      <SlideFrame
        layout="bullets"
        surface="light"
        part={2}
        number={3}
        total={16}
        parts={['The cost', 'Slice it', 'Ship it']}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={view}
        deepDive={false}
        titleStamp={null}
        {...props}
      >
        <h2>Every slip we had last year started as a big batch</h2>
      </SlideFrame>
    </div>
  );
}

// What a token resolves to as a background colour, read off the frame.
async function resolved(page: Page, token: string) {
  return page.getByTestId('slide-frame').evaluate((el, name) => {
    const probe = document.createElement('i');
    probe.style.background = `var(--sw-${name})`;
    el.appendChild(probe);
    const colour = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return colour;
  }, token);
}

test('is 8 px of accent', async ({ mount, page }) => {
  await mount(slide(look('accent')));

  const bar = page.getByTestId('top-bar');
  await expect(bar).toHaveCSS('height', '8px');
  await expect(bar).toHaveCSS('width', '1920px');
  await expect(bar).toHaveCSS('background-color', await resolved(page, 'accent'));
  const box = await bar.boundingBox();
  expect(box?.y).toBe(0);
});

test('renders nothing when the look says none', async ({ mount, page }) => {
  await mount(slide(look('none')));

  await expect(page.getByTestId('top-bar')).toHaveCount(0);
});

test('hides on a title slide', async ({ mount, page }) => {
  for (const layout of ['title', 'divider', 'close', 'join'] as const) {
    const component = await mount(slide(look('accent'), { layout, surface: 'dark', part: null }));
    await expect(page.getByTestId('top-bar')).toBeHidden();
    await component.unmount();
  }
});

test('hides under the deep-dive strip', async ({ mount, page }) => {
  await mount(slide(look('accent'), { deepDive: true }));

  await expect(page.getByTestId('top-bar')).toBeHidden();
});

test('progress bar ends in accent-2', async ({ mount, page }) => {
  await mount(slide(look('none'), {}, { '--sw-accent-2': 'var(--sw-good)' }));

  const good = await resolved(page, 'good');
  const image = await page
    .getByTestId('slide-progress')
    .evaluate((el) => getComputedStyle(el).backgroundImage);
  expect(image).toContain('linear-gradient');
  expect(image.trimEnd().endsWith(`${good} 100%)`) || image.includes(good)).toBe(true);
  expect(await resolved(page, 'accent')).not.toBe(good);
});

test('progress bar on a dark content slide stays the accent glow', async ({ mount, page }) => {
  await mount(slide(look('none'), { surface: 'dark' }));

  const bar = page.getByTestId('slide-progress');
  await expect(bar).toHaveCSS('background-color', await resolved(page, 'accent-glow'));
  await expect(bar).toHaveCSS('background-image', 'none');
  await expect(bar).toHaveCSS('opacity', '0.7');
});

test('screenshot: content slide with the top bar', async ({ mount, page }) => {
  await mount(slide(look('accent')));

  await expect(page).toHaveScreenshot('content-slide-top-bar.png');
});
