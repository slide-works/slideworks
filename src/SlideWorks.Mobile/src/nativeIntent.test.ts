import { describe, expect, it } from 'vitest';
import { redirectSystemPath } from '../app/+native-intent';
import { HOSTED_SERVER, links } from './links';

const hostedJoin = String(links.join('123456', HOSTED_SERVER));
const route = (path: string, initial = false) => redirectSystemPath({ path, initial });

describe('native intent', () => {
  it('maps join links to /join on the hosted server', () => {
    expect(route('https://slideworks.app/j/123456')).toBe(hostedJoin);
    expect(route('https://slideworks.live/j/123456')).toBe(hostedJoin);
    expect(route('https://slideworks.live/123456', true)).toBe(hostedJoin);
    // D16: the public host claims only /j/*, so its six-character SPA routes stay in the browser.
    expect(route('https://slideworks.app/123456')).toBe('https://slideworks.app/123456');
    expect(route('https://slideworks.live/decks')).toBe('https://slideworks.live/decks');
  });

  it('passes through links with trailing segments', () => {
    for (const url of [
      'slideworks://join/123456/extra',
      'https://slideworks.app/j/123456/extra',
      'https://slideworks.live/j/123456/extra',
      'https://slideworks.live/123456/extra',
      'https://slideworks.live/1234567',
    ]) {
      expect(route(url)).toBe(url);
    }
  });

  it('keeps a custom-scheme server', () => {
    expect(route('slideworks://join/654321?server=http%3A%2F%2F10.0.2.2%3A5510')).toBe(
      String(links.join('654321', 'http://10.0.2.2:5510')),
    );
    expect(route('slideworks://join/654321')).toBe(String(links.join('654321')));
  });

  it('swallows signed-in', () => {
    expect(
      route('slideworks://signed-in?code=c&state=s&iss=https%3A%2F%2Fslideworks.app'),
    ).toBeNull();
    expect(route('slideworks://signed-in?error=cancelled', true)).toBe(String(links.home()));
  });
});
