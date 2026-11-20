// A YouTube link becomes a video id and a start time, or nothing. The player
// builds the embed address and the "Watch on YouTube" link from those two
// values. The text of the slot never becomes an iframe src or an href.
// The server runs the same rules; src/contracts/youtube-cases.json holds the cases.

import { safeHref } from './safeHref';

export type YouTubeVideo = { id: string; start: number };

const HOSTS = new Set([
  'youtube.com',
  'www.youtube.com',
  'm.youtube.com',
  'music.youtube.com',
  'youtube-nocookie.com',
  'www.youtube-nocookie.com',
  'youtu.be',
  'www.youtu.be',
]);

const SHORT_HOSTS = new Set(['youtu.be', 'www.youtu.be']);
const MAX_START_SECONDS = 86400;

export function youtubeVideo(value: string): YouTubeVideo | undefined {
  if (typeof value !== 'string' || safeHref(value) === undefined) return undefined;
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return undefined;
  }
  const host = url.hostname.toLowerCase();
  if (!HOSTS.has(host)) return undefined;
  const segments = url.pathname.split('/').filter((segment) => segment.length > 0);
  const id = readId(host, segments, url);
  if (id === undefined) return undefined;
  return { id, start: startOf(url) };
}

// The embed address for this video. autoplay is set only after a click, or when
// the slide itself is set to play on show. A bad id returns nothing.
export function youtubeEmbedSrc(video: YouTubeVideo, autoplay: boolean): string | undefined {
  const id = videoId(video.id);
  if (id === undefined) return undefined;
  const params = ['rel=0', 'modestbranding=1', 'playsinline=1'];
  if (autoplay) params.push('autoplay=1');
  const start = startSeconds(video.start);
  if (start > 0) params.push(`start=${start}`);
  return `https://www.youtube-nocookie.com/embed/${id}?${params.join('&')}`;
}

// The YouTube watch page for this video, which is the link under the player.
export function youtubeWatchHref(video: YouTubeVideo): string | undefined {
  const id = videoId(video.id);
  if (id === undefined) return undefined;
  const start = startSeconds(video.start);
  return start > 0
    ? `https://www.youtube.com/watch?v=${id}&t=${start}`
    : `https://www.youtube.com/watch?v=${id}`;
}

// YouTube's poster for this video. hqdefault exists for every video id.
export function youtubePosterSrc(video: YouTubeVideo): string | undefined {
  const id = videoId(video.id);
  return id === undefined ? undefined : `https://i.ytimg.com/vi/${id}/hqdefault.jpg`;
}

function readId(host: string, segments: string[], url: URL): string | undefined {
  if (SHORT_HOSTS.has(host)) {
    return segments.length === 1 ? videoId(segments[0]) : undefined;
  }
  if (segments.length === 1 && segments[0] === 'watch') return videoId(url.searchParams.get('v'));
  if (
    segments.length === 2 &&
    (segments[0] === 'embed' ||
      segments[0] === 'shorts' ||
      segments[0] === 'live' ||
      segments[0] === 'v')
  ) {
    return videoId(segments[1]);
  }
  return undefined;
}

// start= wins over t=. A time that is not a number of seconds or 1h2m3s is ignored.
function startOf(url: URL): number {
  const raw = url.searchParams.get('start') ?? url.searchParams.get('t');
  return raw === null ? 0 : parseTime(raw);
}

function parseTime(raw: string): number {
  if (raw.length === 0 || raw.length > 32) return 0;
  if (digits(raw)) return clamp(Number(raw));
  let total = 0;
  let index = 0;
  let any = false;
  while (index < raw.length) {
    const start = index;
    while (index < raw.length && isDigit(raw.charCodeAt(index))) index++;
    if (index === start || index >= raw.length) return 0;
    const unit = raw.charAt(index);
    index++;
    if (unit !== 'h' && unit !== 'm' && unit !== 's') return 0;
    const number = Number(raw.slice(start, index - 1));
    if (!Number.isFinite(number)) return 0;
    any = true;
    let scale = 1;
    if (unit === 'h') scale = 3600;
    else if (unit === 'm') scale = 60;
    total += number * scale;
    if (total >= MAX_START_SECONDS) return MAX_START_SECONDS;
  }
  return any ? clamp(total) : 0;
}

function videoId(value: string | null): string | undefined {
  if (value === null || value.length !== 11) return undefined;
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    const ok =
      isDigit(code) ||
      (code >= 65 && code <= 90) ||
      (code >= 97 && code <= 122) ||
      code === 95 ||
      code === 45;
    if (!ok) return undefined;
  }
  return value;
}

function startSeconds(value: number): number {
  if (!Number.isInteger(value) || value <= 0) return 0;
  return value >= MAX_START_SECONDS ? MAX_START_SECONDS : value;
}

function digits(value: string): boolean {
  for (let i = 0; i < value.length; i++) {
    if (!isDigit(value.charCodeAt(i))) return false;
  }
  return value.length > 0;
}

const isDigit = (code: number) => code >= 48 && code <= 57;

function clamp(seconds: number): number {
  if (!Number.isFinite(seconds) || seconds <= 0) return 0;
  return seconds >= MAX_START_SECONDS ? MAX_START_SECONDS : Math.floor(seconds);
}
