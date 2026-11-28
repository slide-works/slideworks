import { readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// expo-router makes every file under app/ a route, so a spec, helper or component there would become a screen.
const appDir = new URL('../app/', import.meta.url);
const routeFile = /^[^.]+\.tsx?$/;
const pascalCase = /^[A-Z]/;
// Folders may only be route groups `(name)` or dynamic segments `[name]`, never components/, hooks/ or lib/.
const routeFolder = /^(\([^)]+\)|\[[^\]]+\])$/;

describe('layout', () => {
  it('app holds only routes', () => {
    const entries = readdirSync(appDir, { recursive: true, withFileTypes: true });
    const files = entries.filter((entry) => entry.isFile()).map((entry) => entry.name);

    expect(files.length).toBeGreaterThan(0);
    expect(files.filter((name) => !routeFile.test(name) || pascalCase.test(name))).toEqual([]);
    expect(
      entries
        .filter((entry) => entry.isDirectory())
        .map((entry) => entry.name)
        .filter((name) => !routeFolder.test(name)),
    ).toEqual([]);
  });
});
