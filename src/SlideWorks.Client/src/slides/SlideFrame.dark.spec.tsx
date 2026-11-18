import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { lookStyle } from '../look/lookStyle';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { Kicker } from './blocks/Kicker';
import { SlideFrame, type SlideFrameProps } from './SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

// A slide under a palette, through lookStyle as SlideRenderer applies it.
function themed(palette: string, props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  const look = lookViewOf('talk', palette);
  return (
    <div style={lookStyle(look)}>
      <SlideFrame
        layout="bullets"
        surface="dark"
        part={2}
        parts={['The cost', 'Slice it', 'Ship it', 'Make it stick']}
        number={3}
        total={16}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={look}
        deepDive={false}
        titleStamp={null}
        {...props}
      >
        <Kicker text="The cost" testId="kicker" />
        <h2>Every slip we had last year started as a big batch</h2>
      </SlideFrame>
    </div>
  );
}

// A token as the browser resolves it to a colour, read through a probe element in the frame.
async function resolved(page: Page, token: string) {
  return page.getByTestId('slide-frame').evaluate((frame, name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    frame.append(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  }, token);
}

for (const palette of ['cobalt', 'ember']) {
  test(`dark content slide keeps tracker and footer under ${palette}`, async ({ mount, page }) => {
    await mount(themed(palette));

    const frame = page.getByTestId('slide-frame');
    await expect(frame).toHaveAttribute('data-surface', 'dark');
    await expect(frame).toHaveAttribute('data-dark', 'false');
    const parts = page.getByTestId('part-tracker-part');
    await expect(parts).toHaveCount(4);
    const muted = await resolved(page, '--sw-night-muted');
    await expect(parts.nth(0)).toHaveCSS('color', muted);
    await expect(parts.nth(2)).toHaveCSS('color', muted);
    await expect(parts.nth(1)).toHaveCSS('color', await resolved(page, '--sw-accent-glow'));
    await expect(page.getByTestId('slide-footer-text')).toHaveCSS('color', muted);
    await expect(page.getByTestId('slide-footer-text')).toHaveText(
      '3 / 16 · Ship smaller · Sam Rivera',
    );
  });

  test(`screenshot: dark content slide ${palette}`, async ({ mount, page }) => {
    await mount(themed(palette));
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(
      `slide-frame-dark-content-${palette}.png`,
    );
  });
}

test('dark kicker is accent-glow', async ({ mount, page }) => {
  await mount(themed('cobalt'));
  await expect(page.getByTestId('kicker')).toHaveCSS(
    'color',
    await resolved(page, '--sw-accent-glow'),
  );
});

test('a deep dive keeps its own kicker colour on night', async ({ mount, page }) => {
  await mount(themed('cobalt', { deepDive: true }));
  await expect(page.getByTestId('kicker')).toHaveCSS(
    'color',
    await resolved(page, '--sw-dd-on-night'),
  );
});

test('a light content slide keeps the light kicker', async ({ mount, page }) => {
  await mount(themed('cobalt', { surface: 'light' }));
  await expect(page.getByTestId('kicker')).toHaveCSS('color', await resolved(page, '--sw-accent'));
});

test('a title slide draws as before', async ({ mount, page }) => {
  await mount(themed('cobalt', { layout: 'title', part: null, number: 1 }));

  const frame = page.getByTestId('slide-frame');
  await expect(frame).toHaveAttribute('data-dark', 'true');
  await expect(frame).toHaveCSS('background-color', await resolved(page, '--sw-night'));
  await expect(page.getByTestId('part-tracker-part')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer-text')).toHaveCount(0);
  await expect(page.getByTestId('slide-progress')).toHaveCSS('opacity', '0.7');
});
