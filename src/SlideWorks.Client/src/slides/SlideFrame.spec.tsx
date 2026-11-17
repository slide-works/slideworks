import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { LookView, TemplateChrome } from '../api/types';
import { lookStyle } from '../look/lookStyle';
import { FOOTER_DATE_LABEL, lookViewOf } from '../test-support/fixtures/lookViewOf';
import { Stamp } from './blocks/Stamp';
import { SlideFrame, type SlideFrameProps } from './SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

const deck = {
  parts: ['The cost', 'Slice it', 'Ship it', 'Make it stick'],
  deckTitle: 'Ship smaller',
  author: 'Sam Rivera',
  look: lookViewOf('talk', 'cobalt'),
  deepDive: false,
  titleStamp: null,
};

// A plain function, not a component: CT can only mount components from source files.
function slide(props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  return (
    <SlideFrame layout="bullets" surface="light" part={2} number={3} total={16} {...deck} {...props}>
      <h2>Every slip we had last year started as a big batch</h2>
    </SlideFrame>
  );
}

test('content slide shows tracker with current part and footer', async ({ mount, page }) => {
  await mount(slide());

  const frame = page.getByTestId('slide-frame');
  await expect(frame).toHaveAttribute('data-dark', 'false');
  await expect(frame).toHaveAttribute('data-layout', 'bullets');

  const parts = page.getByTestId('part-tracker-part');
  await expect(parts).toHaveText(deck.parts);
  await expect(parts.nth(0)).toHaveAttribute('data-state', 'past');
  await expect(parts.nth(1)).toHaveAttribute('data-state', 'on');
  await expect(parts.nth(1)).toHaveAttribute('aria-current', 'step');
  await expect(parts.nth(2)).toHaveAttribute('data-state', 'next');
  await expect(parts.nth(3)).toHaveAttribute('data-state', 'next');
  await expect(page.locator('[aria-current]')).toHaveCount(1);

  await expect(page.getByTestId('slide-footer-text')).toHaveText(
    '3 / 16 · Ship smaller · Sam Rivera',
  );
});

test('title slide is dark without tracker or footer', async ({ mount, page }) => {
  await mount(slide({ layout: 'title', surface: 'dark', part: null, number: 1 }));

  const frame = page.getByTestId('slide-frame');
  await expect(frame).toHaveAttribute('data-dark', 'true');
  const nightInk = await frame.evaluate((element) => {
    const probe = document.createElement('span');
    probe.style.color = 'var(--sw-night-ink)';
    element.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  });
  await expect(frame).toHaveCSS('color', nightInk);
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer')).toHaveCount(0);
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);
});

test('divider and close slides are dark without tracker or footer', async ({ mount, page }) => {
  for (const layout of ['divider', 'close'] as const) {
    const component = await mount(slide({ layout, surface: 'dark', part: 1 }));
    await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-dark', 'true');
    await expect(page.getByTestId('part-tracker')).toHaveCount(0);
    await expect(page.getByTestId('slide-footer')).toHaveCount(0);
    await component.unmount();
  }
});

test('deck without parts shows no tracker', async ({ mount, page }) => {
  await mount(slide({ parts: [], part: null }));

  await expect(page.getByTestId('slide-frame')).toBeAttached();
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer-text')).toHaveText(
    '3 / 16 · Ship smaller · Sam Rivera',
  );
});

test('slide outside any part highlights no part', async ({ mount, page }) => {
  await mount(slide({ part: null }));

  await expect(page.getByTestId('part-tracker-part')).toHaveCount(4);
  await expect(page.locator('[data-testid="part-tracker-part"][data-state="next"]')).toHaveCount(4);
  await expect(page.locator('[aria-current]')).toHaveCount(0);
});

test('footer leaves out a missing author', async ({ mount, page }) => {
  await mount(slide({ author: null }));

  await expect(page.getByTestId('slide-footer-text')).toHaveText('3 / 16 · Ship smaller');
});

test('progress reflects slide number', async ({ mount, page }) => {
  const component = await mount(slide({ number: 3, total: 16 }));
  const progress = page.getByTestId('slide-progress');
  await expect(progress).toHaveCSS('width', '360px');

  await component.update(slide({ number: 16, total: 16 }));
  await expect(progress).toHaveCSS('width', '1920px');
});

test('chrome sits where the layouts expect it, in canvas px', async ({ mount, page }) => {
  await mount(slide());

  expect(await page.getByTestId('slide-frame').boundingBox()).toMatchObject({
    x: 0,
    y: 0,
    width: 1920,
    height: 1080,
  });
  // The footer's text line starts at y = 1023; layout bodies may run to y = 996.
  expect(await page.getByTestId('slide-footer').boundingBox()).toMatchObject({
    x: 120,
    y: 1023,
    width: 1680,
    height: 17,
  });
  expect(await page.getByTestId('part-tracker').boundingBox()).toMatchObject({
    x: 120,
    y: 40,
    width: 1680,
  });
  expect(await page.getByTestId('slide-progress').boundingBox()).toMatchObject({
    x: 0,
    y: 1075,
    height: 5,
  });
});

// A content layout built from the seam: a 180 px header (42 px kicker and
// 138 px headline), then a body whose 620 px block (SlideFit.BODY_HEIGHT)
// runs to 150 + 180 + 46 + 620 = 996.
function seamLayout() {
  return (
    <SlideFrame layout="bullets" surface="light" part={2} number={3} total={16} {...deck}>
      <div className="sw-layout" data-testid="seam-layout">
        <div style={{ height: 180, flexShrink: 0 }} />
        <div className="sw-body" data-testid="seam-body">
          <div style={{ height: 620 }} data-testid="seam-content" />
        </div>
      </div>
    </SlideFrame>
  );
}

test('layout root fills the content box and the body runs to y = 996', async ({ mount, page }) => {
  await mount(seamLayout());

  expect(await page.getByTestId('seam-layout').boundingBox()).toEqual({
    x: 120,
    y: 150,
    width: 1680,
    height: 810,
  });
  const content = await page.getByTestId('seam-content').boundingBox();
  expect(content).toMatchObject({ y: 376, height: 620 });
  const contentBottom = content!.y + content!.height;
  expect(contentBottom).toBe(996);

  // Not clipped: the point just above the body's end is the body's content.
  const hit = await page.evaluate(
    () => document.elementFromPoint(960, 990)?.getAttribute('data-testid') ?? null,
  );
  expect(hit).toBe('seam-content');

  const footer = await page.getByTestId('slide-footer').boundingBox();
  expect(contentBottom).toBeLessThan(footer!.y);
  expect(footer!.y).toBe(1023);
});

// The talk look with some of its chrome overridden, as the server sends a
// deck's effective chrome.
function talkWith(chrome: Partial<TemplateChrome>, look: Partial<LookView> = {}): LookView {
  const talk = lookViewOf('talk', 'cobalt', look);
  return {
    ...talk,
    template: { ...talk.template, chrome: { ...talk.template.chrome, ...chrome } },
  };
}

// The computed colour a token resolves to inside the frame.
async function tokenColour(page: Page, token: string) {
  return page.getByTestId('slide-frame').evaluate((element, name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    element.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  }, token);
}

test('talk shows the tracker, an author footer and progress', async ({ mount, page }) => {
  await mount(slide({ look: lookViewOf('talk', 'cobalt') }));

  await expect(page.getByTestId('part-tracker-part')).toHaveCount(4);
  await expect(page.getByTestId('slide-footer-text')).toHaveText(
    '3 / 16 · Ship smaller · Sam Rivera',
  );
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);
});

test('update has no tracker and a date footer', async ({ mount, page }) => {
  await mount(slide({ look: lookViewOf('update', 'cobalt') }));

  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer-text')).toHaveText(
    `3 / 16 · Ship smaller · ${FOOTER_DATE_LABEL}`,
  );
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);
});

test('a date footer shows the date even without an author', async ({ mount, page }) => {
  await mount(slide({ look: talkWith({ footer: 'date' }), author: null }));

  await expect(page.getByTestId('slide-footer-text')).toHaveText(
    `3 / 16 · Ship smaller · ${FOOTER_DATE_LABEL}`,
  );
});

test('overrides turn each part of the chrome off', async ({ mount, page }) => {
  const component = await mount(slide({ look: talkWith({ tracker: false }) }));
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer')).toHaveCount(1);
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);

  await component.update(slide({ look: talkWith({ footer: 'off' }) }));
  await expect(page.getByTestId('part-tracker')).toHaveCount(1);
  await expect(page.getByTestId('slide-footer')).toHaveCount(0);
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);

  await component.update(slide({ look: talkWith({ progress: false }) }));
  await expect(page.getByTestId('part-tracker')).toHaveCount(1);
  await expect(page.getByTestId('slide-footer')).toHaveCount(1);
  await expect(page.getByTestId('slide-progress')).toHaveCount(0);
});

test('a draft deck has the stamp on its title slide and a chip in the footer', async ({
  mount,
  page,
}) => {
  const look = talkWith({}, { draft: true });
  const component = await mount(slide({ look }));
  await expect(page.getByTestId('draft-chip')).toHaveText('Draft');
  await expect(page.getByTestId('block-stamp')).toHaveCount(0);
  // The chip sits on the footer's line, which stays at y = 1023.
  expect(await page.getByTestId('slide-footer').boundingBox()).toMatchObject({ y: 1023 });

  await component.update(slide({ look, layout: 'title', surface: 'dark', part: null, number: 1 }));
  await expect(page.getByTestId('block-stamp')).toHaveText('Draft');
  await expect(page.getByTestId('draft-chip')).toHaveCount(0);
});

test('a title slide with its own stamp shows only that one', async ({ mount, page }) => {
  await mount(
    <SlideFrame
      layout="title"
      surface="dark"
      part={null}
      number={1}
      total={16}
      {...deck}
      look={talkWith({}, { draft: true })}
      titleStamp="Internal"
    >
      <Stamp text="Internal" />
    </SlideFrame>,
  );

  await expect(page.getByTestId('block-stamp')).toHaveCount(1);
  await expect(page.getByTestId('block-stamp')).toHaveText('Internal');
});

test('a deck that is not a draft has no stamp or chip', async ({ mount, page }) => {
  const component = await mount(slide());
  await expect(page.getByTestId('draft-chip')).toHaveCount(0);

  await component.update(slide({ layout: 'title', surface: 'dark', part: null, number: 1 }));
  await expect(page.getByTestId('block-stamp')).toHaveCount(0);
});

test('a dark slide has no footer, even with a logo or a draft', async ({ mount, page }) => {
  await mount(
    slide({
      layout: 'divider',
      surface: 'dark',
      part: 1,
      look: talkWith({}, { draft: true, logo: { url: '/api/images/logo-1' } }),
    }),
  );

  await expect(page.getByTestId('slide-footer')).toHaveCount(0);
  await expect(page.getByTestId('slide-logo')).toHaveCount(0);
  await expect(page.getByTestId('draft-chip')).toHaveCount(0);
});

test('a deep dive shows the pill and the deep-dive top bar', async ({ mount, page }) => {
  const component = await mount(slide());
  const frame = page.getByTestId('slide-frame');
  await expect(page.getByTestId('deep-dive-pill')).toHaveCount(0);
  const accent = await tokenColour(page, '--sw-accent');
  expect(await frame.evaluate((e) => getComputedStyle(e, '::before').backgroundColor)).toBe(accent);

  await component.update(slide({ deepDive: true }));
  await expect(frame).toHaveAttribute('data-deep-dive', 'true');
  const pill = page.getByTestId('deep-dive-pill');
  await expect(pill).toHaveText('Deep dive');
  await expect(pill).toHaveCSS('background-color', await tokenColour(page, '--sw-dd'));
  await expect(pill).toHaveCSS('color', await tokenColour(page, '--sw-on-dd'));
  const bar = await frame.evaluate((e) => {
    const style = getComputedStyle(e, '::before');
    return { background: style.backgroundColor, height: style.height };
  });
  expect(bar).toEqual({ background: await tokenColour(page, '--sw-dd'), height: '12px' });

  // Between the tracker and the content box.
  const box = await pill.boundingBox();
  expect(box).toMatchObject({ x: 120, y: 98 });
  expect(box!.y + box!.height).toBeLessThanOrEqual(150);
});

test('the logo renders at the end of the footer, after the draft chip', async ({ mount, page }) => {
  await mount(slide({ look: talkWith({}, { draft: true, logo: { url: '/api/images/logo-1' } }) }));

  const logo = page.getByTestId('slide-logo');
  await expect(logo).toHaveAttribute('src', '/api/images/logo-1');
  await expect(logo).toHaveCSS('height', '34px');
  const chip = await page.getByTestId('draft-chip').boundingBox();
  const logoBox = await logo.boundingBox();
  expect(chip!.x).toBeLessThan(logoBox!.x);
  // The footer's text line stays at y = 1023 with the logo in it.
  expect(await page.getByTestId('slide-footer').boundingBox()).toMatchObject({
    y: 1023,
    height: 17,
  });
});

test('a footer line too long for its row ends in an ellipsis on one line', async ({
  mount,
  page,
}) => {
  // The longest title and author the server accepts, on a draft deck with a logo.
  await mount(
    slide({
      deckTitle: 'T'.repeat(120),
      author: 'A'.repeat(80),
      look: talkWith({}, { draft: true, logo: { url: '/api/images/logo-1' } }),
    }),
  );

  const text = page.getByTestId('slide-footer-text');
  const box = await text.boundingBox();
  expect(box).toMatchObject({ y: 1023, height: 17 });
  const chip = await page.getByTestId('draft-chip').boundingBox();
  expect(box!.x + box!.width).toBeLessThanOrEqual(chip!.x);
  await expect(text).toHaveCSS('text-overflow', 'ellipsis');
  expect(await text.evaluate((e) => e.scrollWidth > e.clientWidth)).toBe(true);
});

test('a dark slide draws the progress bar in the accent glow, softened', async ({
  mount,
  page,
}) => {
  const component = await mount(slide());
  const bar = page.getByTestId('slide-progress');
  await expect(bar).toHaveCSS('background-color', await tokenColour(page, '--sw-accent'));
  await expect(bar).toHaveCSS('opacity', '1');

  await component.update(slide({ layout: 'title', surface: 'dark', part: null, number: 1 }));
  await expect(bar).toHaveCSS('background-color', await tokenColour(page, '--sw-accent-glow'));
  await expect(bar).toHaveCSS('opacity', '0.7');
});

// A slide under a palette, through lookStyle as SlideRenderer applies it.
function themed(palette: string, props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  const look = lookViewOf('talk', palette);
  return <div style={lookStyle(look)}>{slide({ look, ...props })}</div>;
}

for (const palette of ['cobalt', 'ember']) {
  test(`screenshot: content slide under ${palette}`, async ({ mount, page }) => {
    await mount(themed(palette));
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(
      `slide-frame-content-${palette}.png`,
    );
  });

  test(`screenshot: deep dive under ${palette}`, async ({ mount, page }) => {
    await mount(themed(palette, { deepDive: true }));
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(
      `slide-frame-deep-dive-${palette}.png`,
    );
  });
}

test('screenshot: content and title chrome', async ({ mount, page }) => {
  await mount(
    <div>
      {slide()}
      {slide({ layout: 'title', surface: 'dark', part: null, number: 1 })}
    </div>,
  );
  const frames = page.getByTestId('slide-frame');
  await expect(frames.nth(0)).toHaveScreenshot('slide-frame-content.png');
  await expect(frames.nth(1)).toHaveScreenshot('slide-frame-title.png');
});
