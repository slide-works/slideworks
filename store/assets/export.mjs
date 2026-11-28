#!/usr/bin/env node
// Exports every store image from its asset.html to store/assets/export/<set>/<n>-<screen>.png, then copies the
// app-icon set over the app's icon and splash files (src/SlideWorks.Mobile/assets).
//
//   node store/assets/export.mjs [set ...]
//
// Uses the Playwright the SPA already installs (resolved from src/SlideWorks.Client/node_modules) and its fonts.
// Run `cd src && pnpm install --frozen-lockfile` first. Each element marked data-shot is one PNG.
import { createRequire } from 'node:module';
import { copyFileSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '../..');
const appAssets = join(repo, 'src/SlideWorks.Mobile/assets');
const { chromium } = createRequire(join(repo, 'src/SlideWorks.Client/package.json'))('playwright');

// Exact pixel size of every image in a set, by element order. A set missing here is not exported.
const five = (w, h) => Array.from({ length: 5 }, () => [w, h]);
const SETS = {
  'ios-6.9': five(1320, 2868),
  'ios-6.5': five(1284, 2778),
  'ios-6.3': five(1206, 2622),
  'android-phone': five(1080, 1920),
  'android-icon': [[512, 512]],
  'android-feature': [[1024, 500]],
  'app-icon': [[1024, 1024], [1024, 1024], [1024, 1024], [1024, 1024], [1024, 1024], [1024, 1024], [512, 512], [512, 512]],
};

const wanted = process.argv.slice(2);
const names = wanted.length ? wanted : Object.keys(SETS);
const unknown = names.filter((n) => !SETS[n]);
if (unknown.length) {
  console.error(`export: unknown set ${unknown.join(', ')}; known: ${Object.keys(SETS).join(', ')}`);
  process.exit(2);
}

const browser = await chromium.launch();
try {
  for (const set of names) {
    const out = join(here, 'export', set);
    rmSync(out, { recursive: true, force: true });
    mkdirSync(out, { recursive: true });
    const page = await browser.newPage({ viewport: { width: 1400, height: 1000 }, deviceScaleFactor: 1 });
    await page.goto(pathToFileURL(join(here, set, 'asset.html')).href);
    await page.evaluate(() => document.fonts.ready);
    const shots = await page.locator('[data-shot]').all();
    if (shots.length !== SETS[set].length) throw new Error(`${set}: expected ${SETS[set].length} images, the page draws ${shots.length}`);
    for (const [i, shot] of shots.entries()) {
      const id = await shot.getAttribute('data-shot');
      const box = await shot.boundingBox();
      const [w, h] = SETS[set][i];
      if (Math.round(box.width) !== w || Math.round(box.height) !== h) {
        throw new Error(`${set}/${id}: drawn ${box.width}x${box.height}, expected ${w}x${h}`);
      }
      const file = join(out, `${id}.png`);
      await shot.screenshot({ path: file, omitBackground: true });
      const copies = (await shot.getAttribute('data-copy'))?.split(' ') ?? [];
      for (const target of copies) {
        mkdirSync(dirname(join(appAssets, target)), { recursive: true });
        copyFileSync(file, join(appAssets, target));
      }
    }
    await page.close();
    console.log(`export: ${set}: ${readdirSync(out).length} image(s)`);
  }
} finally {
  await browser.close();
}
