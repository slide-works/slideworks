import type { Page } from 'playwright/test';

/** A response held until release(), to land answers out of order. */
export function gate() {
  let release!: () => void;
  const opened = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { opened, release };
}

/**
 * Lets the page settle after a held response is released: the fetch resolves,
 * its JSON is parsed and React renders.
 */
export const settle = (page: Page) =>
  page.evaluate(() => new Promise((done) => setTimeout(done, 200)));
