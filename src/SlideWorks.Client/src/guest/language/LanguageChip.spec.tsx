import { test, expect } from '@playwright/experimental-ct-react';
import { GUEST_TEST_IDS } from '../testIds';
import { LanguageChip } from './LanguageChip';

test('LanguageChip_WhenClicked_ShouldCallOnOpen', async ({ mount, page }) => {
  let opened = 0;
  await mount(
    <LanguageChip short="ไทย" label="Read the slides in Thai" onOpen={() => (opened += 1)} />,
  );
  await page.getByTestId(GUEST_TEST_IDS.languageChip).click();
  await expect.poll(() => opened).toBe(1);
});

test('LanguageChip_WhenRendered_ShouldHaveLabelAsName', async ({ mount, page }) => {
  await mount(<LanguageChip short="ไทย" label="Read the slides in Thai" onOpen={() => {}} />);
  const chip = page.getByRole('button', { name: 'Read the slides in Thai', exact: true });
  await expect(chip).toHaveText('ไทย');
  await expect(chip).toHaveAttribute('data-testid', GUEST_TEST_IDS.languageChip);
});
