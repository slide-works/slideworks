import { readdirSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { links } from './links';

const appDir = fileURLToPath(new URL('../app/', import.meta.url));

// Every screen route under app/ as a URL pattern: groups drop out, [param] is one segment, index is its folder.
function routePatterns(): string[] {
  return readdirSync(appDir, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && /\.tsx$/.test(entry.name) && !entry.name.startsWith('_'))
    .map((entry) => relative(appDir, join(entry.parentPath, entry.name)).replace(/\.tsx$/, ''))
    .map(
      (path) =>
        `/${path
          .split('/')
          .filter((part) => !/^\(.*\)$/.test(part) && part !== 'index')
          .join('/')}`,
    );
}

const everyLink = [
  links.home(),
  links.join('123456'),
  links.join('123456', 'https://slideworks.app'),
  links.signIn(),
  links.talks(),
  links.talk('7Kq2abcdEFGH'),
  links.account(),
].map((href) => String(href).split('?')[0]);

describe('links', () => {
  it('every route has a link', () => {
    const patterns = routePatterns();
    expect(patterns.length).toBe(6);
    for (const pattern of patterns) {
      const matcher = new RegExp(`^${pattern.replace(/\[[^\]]+\]/g, '[^/]+')}$`);
      expect({ pattern, linked: everyLink.some((path) => matcher.test(path)) }).toEqual({
        pattern,
        linked: true,
      });
    }
  });
});
