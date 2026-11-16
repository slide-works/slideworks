import {
  expect,
  type ComponentFixtures,
  type MountResult,
} from '@playwright/experimental-ct-react';
import type { JSX } from 'react';
import type { Page } from 'playwright/test';

// Mounts a player (or a page holding one) and returns once it is listening.
// CT's mount() resolves before React commits, and the player attaches its key
// and hash listeners in effects, so input sent straight after mount() can be
// lost under load. The canvas starts at visibility:hidden and shows only on
// the re-render after the first commit, by which point React has run that
// commit's effects: a visible canvas means the listeners are attached.
export async function mountPlayer(
  mount: ComponentFixtures['mount'],
  page: Page,
  component: JSX.Element,
): Promise<MountResult> {
  const player = await mount(component);
  await expect(page.getByTestId('player-canvas')).toBeVisible();
  return player;
}
