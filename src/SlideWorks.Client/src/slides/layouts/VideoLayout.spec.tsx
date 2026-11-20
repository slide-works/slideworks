import { test, expect } from '@playwright/experimental-ct-react';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import { SlideShowingContext } from '../slideShowing';
import type { SlideDeck } from '../slots';
import type { VideoSlots } from '../slots/video';

test.use({ viewport: { width: 1920, height: 1080 } });

const ID = 'M7lc1UVf-VE';
const POSTER = `https://i.ytimg.com/vi/${ID}/hqdefault.jpg`;
const WATCH = `https://www.youtube.com/watch?v=${ID}`;
const EMBED = `https://www.youtube-nocookie.com/embed/${ID}?rel=0&modestbranding=1&playsinline=1&autoplay=1`;

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function video(slots: VideoSlots): RenderableSlide {
  return { number: 2, layout: 'video', part: 1, deepDive: false, surface: undefined, slots };
}

function render(
  slide: RenderableSlide,
  options: { showing?: boolean; interactive?: boolean } = {},
) {
  return (
    <SlideShowingContext value={options.showing !== false}>
      <SlideRenderer
        slide={slide}
        deck={{ ...deck, interactive: options.interactive }}
        total={sampleDeck.slides.length}
        step={0}
      />
    </SlideShowingContext>
  );
}

const base: VideoSlots = {
  kicker: 'The demo',
  headline: 'The change is easier to see than to describe',
  url: `https://www.youtube.com/watch?v=${ID}&list=PLsecret`,
  caption: 'A two minute walkthrough.',
};

test('play starts the video in the slide and links to YouTube', async ({ mount, page }) => {
  await mount(render(video(base)));

  await expect(page.getByTestId('video-frame')).toHaveCount(0);
  await expect(page.getByTestId('video-poster')).toHaveAttribute('src', POSTER);
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('href', WATCH);
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('target', '_blank');
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('rel', 'noopener noreferrer');
  await expect(page.getByTestId('video-caption')).toHaveText('A two minute walkthrough.');
  await expect(page.locator('[src*="PLsecret"], [href*="PLsecret"]')).toHaveCount(0);

  await page.getByTestId('video-play').click();

  await expect(page.getByTestId('video-frame')).toHaveAttribute('src', EMBED);
  await expect(page.getByTestId('video-frame')).toHaveAttribute('title', base.headline);
  await expect(page.getByTestId('video-poster')).toHaveCount(0);
  await expect(page.locator(`[src*="${base.url}"]`)).toHaveCount(0);
});

test('a start on the slide overrides the time in the link', async ({ mount, page }) => {
  await mount(
    render(
      video({
        headline: 'Watch the slice',
        url: `https://youtu.be/${ID}?t=90`,
        start: 15,
        play: 'auto',
      }),
    ),
  );

  await expect(page.getByTestId('video-frame')).toHaveAttribute('src', `${EMBED}&start=15`);
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('href', `${WATCH}&t=15`);
});

test('a hidden slide shows the poster and drops the player', async ({ mount, page }) => {
  const slide = video({ ...base, play: 'auto' });
  const component = await mount(render(slide, { showing: false }));

  await expect(page.getByTestId('video-frame')).toHaveCount(0);
  await expect(page.getByTestId('video-poster')).toBeVisible();

  await component.update(render(slide, { showing: true }));

  await expect(page.getByTestId('video-frame')).toHaveAttribute('src', EMBED);
});

test('a link that is not YouTube draws no player and no address', async ({ mount, page }) => {
  const url = 'https://evil.example/watch?v=M7lc1UVf-VE';
  await mount(render(video({ headline: 'Watch the slice', url })));

  await expect(page.getByTestId('video-missing')).toHaveText('This link is not a YouTube video.');
  await expect(page.locator('iframe, a, img')).toHaveCount(0);
  await expect(page.getByTestId('layout-video')).not.toContainText(url);
  await expect(page.getByTestId('layout-video')).not.toContainText('javascript:');
});

test('a thumbnail does not play', async ({ mount, page }) => {
  await mount(render(video({ ...base, play: 'auto' }), { interactive: false }));

  await expect(page.getByTestId('video-play')).toHaveCount(0);
  await expect(page.getByTestId('video-frame')).toHaveCount(0);
  await expect(page.getByTestId('video-poster')).toBeVisible();
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('tabindex', '-1');
});
