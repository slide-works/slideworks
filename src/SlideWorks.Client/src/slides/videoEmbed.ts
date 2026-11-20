// A YouTube or Vimeo address becomes an embed the image slide can play. Anything else is not a video.
// The image stays as the poster behind the player.

const YOUTUBE = 'https://www.youtube-nocookie.com/embed/';
const VIMEO = 'https://player.vimeo.com/video/';

export function videoEmbed(value: string | undefined): string | undefined {
  if (typeof value !== 'string') return undefined;
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return undefined;
  }
  if (url.protocol !== 'https:' && url.protocol !== 'http:') return undefined;
  const host = url.hostname.replace(/^www\./, '');
  if (host === 'youtu.be') {
    const id = url.pathname.slice(1).split('/')[0];
    return id ? `${YOUTUBE}${encodeURIComponent(id)}` : undefined;
  }
  if (host === 'youtube.com' || host === 'm.youtube.com') {
    const watch = url.searchParams.get('v');
    if (watch) return `${YOUTUBE}${encodeURIComponent(watch)}`;
    const parts = url.pathname.split('/').filter(Boolean);
    const marker = parts[0];
    if ((marker === 'embed' || marker === 'shorts') && parts[1]) {
      return `${YOUTUBE}${encodeURIComponent(parts[1])}`;
    }
    return undefined;
  }
  if (host === 'vimeo.com') {
    const id = url.pathname.split('/').filter(Boolean)[0];
    return id && /^\d+$/.test(id) ? `${VIMEO}${id}` : undefined;
  }
  return undefined;
}
