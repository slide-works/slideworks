import { readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// expo-router makes every file under app/ a route, so a spec, helper or component there would become a screen.
const appDir = new URL('../app/', import.meta.url);
const routeFile = /^[^.]+\.tsx?$/;
const pascalCase = /^[A-Z]/;
// Folders are route groups `(name)`, dynamic segments `[name]` or lowercase path segments (`join/`, `talk/`).
const routeFolder = /^(\([^)]+\)|\[[^\]]+\]|[a-z0-9-]+)$/;
const helperFolders = new Set(['components', 'hooks', 'lib', 'utils', 'test-support']);

// Returns what is wrong with a path relative to app/, or [] when it is a route file.
function violations(path: string): string[] {
  const parts = path.split('/');
  const name = parts[parts.length - 1];
  const bad: string[] = [];
  if (!routeFile.test(name) || pascalCase.test(name)) bad.push(`${path}: not a route file name`);
  for (const folder of parts.slice(0, -1)) {
    if (helperFolders.has(folder) || !routeFolder.test(folder))
      bad.push(`${path}: ${folder}/ is not a route folder`);
  }
  return bad;
}

describe('layout', () => {
  it('app holds only routes', () => {
    const files = readdirSync(appDir, { recursive: true, withFileTypes: true })
      .filter((entry) => entry.isFile())
      .map((entry) =>
        entry.parentPath.slice(appDir.pathname.length).concat(entry.name).replace(/^\/?/, ''),
      );

    expect(files.length).toBeGreaterThan(0);
    expect(files.flatMap(violations)).toEqual([]);
  });

  it.each(['index.tsx', '_layout.tsx', 'join/[code].tsx', 'talk/[id].tsx', '(tabs)/index.tsx'])(
    'accepts %s',
    (path) => {
      expect(violations(path)).toEqual([]);
    },
  );

  it.each([
    'components/Button.tsx',
    'hooks/useThing.ts',
    'lib/api.ts',
    'utils/format.ts',
    'test-support/fake.ts',
    'join/Card.tsx',
    'index.spec.tsx',
    'Button.tsx',
  ])('rejects %s', (path) => {
    expect(violations(path)).not.toEqual([]);
  });
});
