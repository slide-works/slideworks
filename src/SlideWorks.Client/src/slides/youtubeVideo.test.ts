import youtubeCases from '@contracts/youtube-cases.json';
import { describe, expect, it } from 'vitest';
import { youtubeEmbedSrc, youtubePosterSrc, youtubeVideo, youtubeWatchHref } from './youtubeVideo';

const cases: { value: string; id: string | null; start?: number }[] = youtubeCases.cases;

const VIDEO = { id: 'M7lc1UVf-VE', start: 0 };

describe('youtubeVideo', () => {
  it('has every shared case', () => {
    expect(cases.length).toBeGreaterThanOrEqual(40);
    expect(cases.some((item) => item.id !== null)).toBe(true);
    expect(cases.some((item) => item.id === null)).toBe(true);
  });

  it.each(cases.map((item, index) => [index, item.value, item] as const))(
    'matches shared case %i: %s',
    (_, __, item) => {
      const read = youtubeVideo(item.value);
      if (item.id === null) {
        expect(read).toBeUndefined();
      } else {
        expect(read).toEqual({ id: item.id, start: item.start });
      }
    },
  );

  it('builds the embed, the poster and the YouTube link from the id', () => {
    expect(youtubeEmbedSrc(VIDEO, false)).toBe(
      'https://www.youtube-nocookie.com/embed/M7lc1UVf-VE?rel=0&modestbranding=1&playsinline=1',
    );
    expect(youtubeEmbedSrc({ ...VIDEO, start: 15 }, true)).toBe(
      'https://www.youtube-nocookie.com/embed/M7lc1UVf-VE?rel=0&modestbranding=1&playsinline=1&autoplay=1&start=15',
    );
    expect(youtubeWatchHref(VIDEO)).toBe('https://www.youtube.com/watch?v=M7lc1UVf-VE');
    expect(youtubeWatchHref({ ...VIDEO, start: 15 })).toBe(
      'https://www.youtube.com/watch?v=M7lc1UVf-VE&t=15',
    );
    expect(youtubePosterSrc(VIDEO)).toBe('https://i.ytimg.com/vi/M7lc1UVf-VE/hqdefault.jpg');
  });

  it('refuses to build an address from an id it did not check', () => {
    const bad = { id: 'not a video', start: 0 };
    expect(youtubeEmbedSrc(bad, true)).toBeUndefined();
    expect(youtubeWatchHref(bad)).toBeUndefined();
    expect(youtubePosterSrc(bad)).toBeUndefined();
  });
});
