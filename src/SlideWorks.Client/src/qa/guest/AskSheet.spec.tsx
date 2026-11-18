import { test, expect } from '@playwright/experimental-ct-react';
import { qaRoom } from '../../test-support/fixtures/qaStates';
import { QA_ASK_SHEET_TEST_IDS, QA_LIST_TEST_IDS, QA_POLL_OPENED_TEST_IDS } from '../testIds';
import { AskBar } from './AskBar';
import { AskSheet } from './AskSheet';
import { AskSheetFixture, type AskSheetFixtureProps } from './AskSheetFixture';
import { QuestionsFixture, type QuestionsFixtureProps } from './QuestionsFixture';

// The ask sheet (Q8; mockup phone states 4, 5, 9, 11) over the two fixture providers on a 360 px phone.
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

type SheetProps = Omit<QuestionsFixtureProps, 'children'> & Omit<AskSheetFixtureProps, 'children'>;

// The Ask bar and the sheet as GuestTabs lays them out in the Questions panel, on the guest page's dark background.
function sheet({
  open = true,
  pollJustOpened,
  onShow,
  onClose,
  onGoToPoll,
  ...questions
}: SheetProps = {}) {
  return (
    <QuestionsFixture {...questions}>
      <AskSheetFixture
        open={open}
        pollJustOpened={pollJustOpened}
        onShow={onShow}
        onClose={onClose}
        onGoToPoll={onGoToPoll}
      >
        <div style={{ height: 700, padding: 16, background: 'var(--sw-night)', display: 'flex' }}>
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'end' }}>
            <AskBar />
          </div>
          <AskSheet presenter="Sam" />
        </div>
      </AskSheetFixture>
    </QuestionsFixture>
  );
}

const ID = QA_ASK_SHEET_TEST_IDS;

test('the counter follows the text and Send is disabled while the trimmed text is empty or over 200', async ({
  mount,
  page,
}) => {
  await mount(sheet());
  const text = page.getByTestId(ID.text);
  const send = page.getByTestId(ID.send);
  await expect(page.getByTestId(ID.counter)).toHaveText('0 / 200');
  await expect(send).toBeDisabled();

  await text.fill('   ');
  await expect(page.getByTestId(ID.counter)).toHaveText('3 / 200');
  await expect(send).toBeDisabled();

  await text.fill('How do you split a big change?');
  await expect(page.getByTestId(ID.counter)).toHaveText('30 / 200');
  await expect(send).toBeEnabled();

  await text.fill('a'.repeat(200));
  await expect(send).toBeEnabled();

  await text.fill('a'.repeat(201));
  await expect(page.getByTestId(ID.counter)).toHaveText('201 / 200');
  await expect(send).toBeDisabled();
  await expect(text).toHaveAttribute('aria-invalid', 'true');
});

test('anonymous by default: Send anonymously sends the trimmed text with no name, and Enter sends too', async ({
  mount,
  page,
}) => {
  const asked: Array<[string, string | null]> = [];
  await mount(sheet({ onAsk: (text, name) => asked.push([text, name]) }));
  await expect(page.getByTestId(ID.title)).toHaveText('Ask a question');
  await expect(page.getByTestId(ID.text)).toHaveAttribute(
    'placeholder',
    'What would you like Sam to answer?',
  );
  await expect(page.getByTestId(ID.hint)).toHaveText('One question at a time works best');
  await expect(page.getByTestId(ID.nameHint)).toHaveText(
    'Off: nobody sees who asked, not even Sam.',
  );
  await expect(page.getByTestId(ID.send)).toHaveText('Send anonymously');
  await expect(page.getByTestId(ID.finePrint)).toHaveText(
    "You can't edit a question after sending it.",
  );
  await expect(page.getByTestId(ID.name)).toHaveCount(0);

  await page.getByTestId(ID.text).fill('  First one  ');
  await page.getByTestId(ID.send).click();
  await page.getByTestId(ID.text).press('Enter');
  expect(asked).toEqual([
    ['First one', null],
    ['First one', null],
  ]);
});

test('with a name: the switch shows the name box, Send names the person, and the name goes with the question', async ({
  mount,
  page,
}) => {
  const asked: Array<[string, string | null]> = [];
  await mount(sheet({ onAsk: (text, name) => asked.push([text, name]) }));
  const nameSwitch = page.getByTestId(ID.nameSwitch);
  await expect(nameSwitch).toHaveAttribute('aria-checked', 'false');
  await nameSwitch.click();
  await expect(nameSwitch).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByTestId(ID.nameHint)).toHaveText(
    'The room and Sam see this name with your question.',
  );
  await expect(page.getByTestId(ID.name)).toHaveAttribute('placeholder', 'Your name');
  // On with no name typed yet: nothing to show, so it still goes anonymously.
  await expect(page.getByTestId(ID.send)).toHaveText('Send anonymously');

  await page.getByTestId(ID.name).fill(' Noor ');
  await expect(page.getByTestId(ID.send)).toHaveText('Send as Noor');
  await page.getByTestId(ID.text).fill('Why not both?');
  await page.getByTestId(ID.send).click();
  expect(asked).toEqual([['Why not both?', 'Noor']]);
});

test('the draft and the typed name survive closing and reopening the sheet', async ({
  mount,
  page,
}) => {
  const component = await mount(sheet());
  await page.getByTestId(ID.nameSwitch).click();
  await page.getByTestId(ID.name).fill('Noor');
  await page.getByTestId(ID.text).fill('Half a question');

  await component.update(sheet({ open: false }));
  await expect(page.getByTestId(ID.sheet)).toHaveCount(0);

  await component.update(sheet({ open: true }));
  await expect(page.getByTestId(ID.text)).toHaveValue('Half a question');
  await expect(page.getByTestId(ID.name)).toHaveValue('Noor');
  await expect(page.getByTestId(ID.send)).toHaveText('Send as Noor');
});

test('names off: no name row, and the question goes anonymously', async ({ mount, page }) => {
  const asked: Array<[string, string | null]> = [];
  await mount(
    sheet({
      room: qaRoom('open', { names: false }),
      onAsk: (text, name) => asked.push([text, name]),
    }),
  );
  await expect(page.getByTestId(ID.nameRow)).toHaveCount(0);
  await expect(page.getByTestId(ID.nameSwitch)).toHaveCount(0);
  await page.getByTestId(ID.text).fill('Anyone there?');
  await page.getByTestId(ID.send).click();
  expect(asked).toEqual([['Anyone there?', null]]);
});

test('sending: the text is read-only, Send is disabled and the state is shown', async ({
  mount,
  page,
}) => {
  await mount(sheet({ asking: 'sending' }));
  await expect(page.getByTestId(ID.sending)).toHaveText('Sending…');
  await expect(page.getByTestId(ID.text)).toHaveJSProperty('readOnly', true);
  await page.getByTestId(ID.text).evaluate((el: HTMLInputElement) => {
    el.readOnly = false;
  });
  await page.getByTestId(ID.text).fill('x');
  await expect(page.getByTestId(ID.send)).toBeDisabled();
});

test('queued: says the question is kept on this phone, and the draft stays', async ({
  mount,
  page,
}) => {
  const component = await mount(sheet());
  await page.getByTestId(ID.text).fill('Sent from the train');
  await component.update(sheet({ asking: 'queued' }));
  await expect(page.getByTestId(ID.queued)).toHaveText(
    'Not sent yet. Your question is kept on this phone and sends by itself when the connection is back.',
  );
  await expect(page.getByTestId(ID.text)).toHaveValue('Sent from the train');
  await expect(page.getByTestId(ID.failed)).toHaveCount(0);
});

test('limited: counts down in seconds, Send is disabled, and no code is shown', async ({
  mount,
  page,
}) => {
  const component = await mount(sheet());
  await page.getByTestId(ID.text).fill('Too keen');
  await component.update(sheet({ asking: 'limited', retryIn: 42, error: 'too_many_questions' }));
  await expect(page.getByTestId(ID.limited)).toHaveText('You can ask again in 42 s');
  await expect(page.getByTestId(ID.send)).toBeDisabled();
  await component.update(sheet({ asking: 'limited', retryIn: 41, error: 'too_many_questions' }));
  await expect(page.getByTestId(ID.limited)).toHaveText('You can ask again in 41 s');
  await expect(page.getByTestId(ID.failed)).toHaveCount(0);
  await expect(page.getByTestId(ID.sheet)).not.toContainText('too_many_questions');

  // The countdown running out sends nothing: the sheet stays and the draft stays.
  await component.update(sheet({ asking: 'idle' }));
  await expect(page.getByTestId(ID.text)).toHaveValue('Too keen');
  await expect(page.getByTestId(ID.send)).toBeEnabled();
});

test('failed: the message is a sentence, not the code, and the draft stays', async ({
  mount,
  page,
}) => {
  const component = await mount(sheet());
  await page.getByTestId(ID.text).fill('Still here');
  await component.update(sheet({ asking: 'failed', error: 'questions_closed' }));
  await expect(page.getByTestId(ID.failed)).toHaveText(
    'Sam has closed questions. You can still upvote.',
  );
  await expect(page.getByTestId(ID.failed)).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(ID.text)).toHaveValue('Still here');
  await expect(page.getByTestId(ID.sheet)).not.toContainText('questions_closed');

  // A 400 with no error member, and a code the copy does not know yet, read as a generic sentence.
  await component.update(sheet({ asking: 'failed', error: 'Request failed (400)' }));
  await expect(page.getByTestId(ID.failed)).toHaveText(
    'Something went wrong on our side. Try again in a moment.',
  );
  await component.update(sheet({ asking: 'failed', error: 'new_round' }));
  await expect(page.getByTestId(ID.failed)).not.toContainText('new_round');
  await expect(page.getByTestId(ID.text)).toHaveValue('Still here');
});

test('a sent question closes the sheet and clears the draft', async ({ mount, page }) => {
  let closed = 0;
  const onClose = () => closed++;
  const component = await mount(sheet({ onClose }));
  await page.getByTestId(ID.text).fill('Going out');
  await page.getByTestId(ID.send).click();
  await component.update(sheet({ onClose, asking: 'sending' }));
  expect(closed).toBe(0);
  await component.update(sheet({ onClose, asking: 'idle' }));
  await expect.poll(() => closed).toBe(1);

  await component.update(sheet({ open: false, onClose }));
  await component.update(sheet({ open: true, onClose }));
  await expect(page.getByTestId(ID.text)).toHaveValue('');
});

test('the poll banner shows only when a poll just opened, and Go to poll asks to leave for it', async ({
  mount,
  page,
}) => {
  let went = 0;
  const component = await mount(sheet());
  await expect(page.getByTestId(QA_POLL_OPENED_TEST_IDS.banner)).toHaveCount(0);

  await component.update(sheet({ pollJustOpened: true, onGoToPoll: () => went++ }));
  await expect(page.getByTestId(QA_POLL_OPENED_TEST_IDS.message)).toHaveText(
    'A poll just opened. It is on the Slide tab.',
  );
  await page.getByTestId(QA_POLL_OPENED_TEST_IDS.goToPoll).click();
  expect(went).toBe(1);
});

test('focus enters the text box when the sheet opens and returns to Ask when it closes', async ({
  mount,
  page,
}) => {
  const component = await mount(sheet({ open: false }));
  await page.getByTestId(QA_LIST_TEST_IDS.ask).click();
  await component.update(sheet({ open: true }));
  await expect(page.getByTestId(ID.text)).toBeFocused();

  await component.update(sheet({ open: false }));
  await expect(page.getByTestId(QA_LIST_TEST_IDS.ask)).toBeFocused();
});

test('the close button and Escape ask to close, and Tab stays inside the sheet', async ({
  mount,
  page,
}) => {
  let closed = 0;
  await mount(sheet({ onClose: () => closed++ }));
  await page.getByTestId(ID.close).click();
  expect(closed).toBe(1);
  await page.getByTestId(ID.text).press('Escape');
  await expect.poll(() => closed).toBe(2);

  await page.getByTestId(ID.text).fill('x');
  await page.getByTestId(ID.send).focus();
  await page.keyboard.press('Tab');
  await expect(page.getByTestId(ID.close)).toBeFocused();
});

test('the sheet fits 360 px with 44 px targets and nothing scrolls sideways', async ({
  mount,
  page,
}) => {
  await mount(sheet({ pollJustOpened: true }));
  await page.getByTestId(ID.nameSwitch).click();
  await page.getByTestId(ID.text).fill('x');
  for (const id of [ID.close, ID.nameSwitch, ID.send, QA_POLL_OPENED_TEST_IDS.goToPoll]) {
    const box = await page.getByTestId(id).boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(44);
  }
  const wide = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(wide).toBe(false);
});

test('screenshot ask sheet empty', async ({ mount, page }) => {
  await mount(sheet());
  await expect(page).toHaveScreenshot('ask-sheet-empty.png');
});

test('screenshot ask sheet with a name', async ({ mount, page }) => {
  await mount(sheet());
  await page.getByTestId(ID.nameSwitch).click();
  await page.getByTestId(ID.name).fill('Noor');
  await page.getByTestId(ID.text).fill('How do you get a team to');
  await expect(page).toHaveScreenshot('ask-sheet-named.png');
});

test('screenshot ask sheet poll just opened', async ({ mount, page }) => {
  await mount(sheet({ pollJustOpened: true }));
  await page.getByTestId(ID.text).fill('How do you get a team to');
  await expect(page).toHaveScreenshot('ask-sheet-poll.png');
});

test('screenshot ask sheet queued and limited', async ({ mount, page }) => {
  const component = await mount(sheet({ asking: 'queued' }));
  await expect(page).toHaveScreenshot('ask-sheet-queued.png');
  await component.update(sheet({ asking: 'limited', retryIn: 42, error: 'too_many_questions' }));
  await expect(page).toHaveScreenshot('ask-sheet-limited.png');
});
