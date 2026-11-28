import { readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// expo-router makes every file under app/ a route, so a spec, helper or component there would become a screen.
const appDir = new URL('../app/', import.meta.url);
const routeFile = /^[^.]+\.tsx?$/;

describe('layout', () => {
  it('app holds only routes', () => {
    const files = readdirSync(appDir, { recursive: true, withFileTypes: true })
      .filter((entry) => entry.isFile())
      .map((entry) => entry.name);

    expect(files.length).toBeGreaterThan(0);
    expect(files.filter((name) => !routeFile.test(name))).toEqual([]);
  });
});
