import { describe, expect, it } from 'vitest';
import { videoEmbed } from './videoEmbed';

describe('videoEmbed', () => {
  it('embeds a YouTube watch address and leaves the image as the poster', () => {
    expect(videoEmbed('https://www.youtube.com/watch?v=dQw4w9WgXcQ')).toBe(
      'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
    );
  });

  it('embeds a Vimeo address', () => {
    expect(videoEmbed('https://vimeo.com/123456')).toBe('https://player.vimeo.com/video/123456');
  });

  it('leaves an ordinary link for the image to open', () => {
    expect(videoEmbed('https://example.com/story')).toBeUndefined();
  });
});
