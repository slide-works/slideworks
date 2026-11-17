import { test, expect } from '@playwright/experimental-ct-react';
import type { Page, Route } from 'playwright/test';
import { LogoField } from './LogoField';
import {
  imageUploadRequest,
  imageUploadResult,
  lookChangeResult,
} from '../../test-support/fixtures/screens';

const deckId = '7Kq2xYp9LmQa';
const imagesPath = `**/api/decks/${deckId}/images`;
const uploadPath = '**/api/uploads/**';
const currentLogo = lookChangeResult.look.logo!;

// Paths are relative to the client project, where the CT runner starts.
const logoFile = 'src/test-support/fixtures/sample-image.png';
const svgFile = 'public/favicon.svg';

type Answer = { status: number; json?: unknown };

// Answers POST /images and the upload PUT, recording each in one ordered log with what reached onChange. The upload
// stub sends no CORS headers and answers no preflight, as the real server doesn't: an OPTIONS request lands in the
// log and fails the order. Each upload entry records whether it went to the page's own origin and its Cookie header
// (allHeaders, because headers() hides Cookie).
async function stubUpload(page: Page, upload: Answer = { status: 204 }) {
  const log: string[] = [];
  await page.route(imagesPath, (route) => {
    const request = route.request();
    log.push(`${request.method()} images ${JSON.stringify(request.postDataJSON())}`);
    return route.fulfill({ status: 201, json: imageUploadResult });
  });
  await page.route(uploadPath, async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const sameOrigin = url.origin === new URL(page.url()).origin;
    const cookie = (await request.allHeaders()).cookie ?? 'no-cookie';
    log.push(
      `${request.method()} ${sameOrigin ? 'same-origin' : url.origin}${url.pathname} ${request.headers()['content-type']} ${Boolean(request.postDataBuffer()?.length)} ${cookie}`,
    );
    return upload.json === undefined
      ? route.fulfill({ status: upload.status })
      : route.fulfill({ status: upload.status, json: upload.json });
  });
  return log;
}

// A signed-in page: a session cookie on the page's origin, which the upload PUT must not send.
async function signIn(page: Page) {
  await page.context().addCookies([{ name: 'sw_session', value: 'signed-in', url: page.url() }]);
}

async function stubLogoImage(page: Page) {
  await page.route(`**${currentLogo.url}`, (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="120" height="32"><rect width="120" height="32" fill="#2563eb"/></svg>',
    }),
  );
}

test('upload posts the image, puts its bytes, then hands the id to onChange', async ({
  mount,
  page,
}) => {
  const log = await stubUpload(page);
  const field = await mount(
    <LogoField deckId={deckId} logo={null} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await signIn(page);
  // The API POST carries the session, so the PUT's missing cookie is credentials 'omit', not a missing session.
  const postCookies: string[] = [];
  page.on('request', async (request) => {
    if (request.method() === 'POST') postCookies.push((await request.allHeaders()).cookie ?? '');
  });

  await expect(field.getByTestId('logo-field-image')).toHaveCount(0);
  await expect(field.getByTestId('logo-field-remove')).toHaveCount(0);
  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect.poll(() => log.length).toBe(3);
  expect(log).toEqual([
    `POST images ${JSON.stringify({ ...imageUploadRequest, name: 'sample-image.png' })}`,
    `PUT same-origin${new URL(imageUploadResult.uploadUrl).pathname} image/png true no-cookie`,
    `onChange ${imageUploadResult.image}`,
  ]);
  expect(postCookies).toEqual(['sw_session=signed-in']);
  await expect(field.getByTestId('logo-field-error')).toHaveCount(0);
  await expect(field.getByTestId('logo-field-file')).toBeEnabled();
});

test('the current logo shows, and remove hands null to onChange', async ({ mount, page }) => {
  await stubLogoImage(page);
  const changes: (string | null)[] = [];
  const field = await mount(
    <LogoField deckId={deckId} logo={currentLogo} onChange={(id) => changes.push(id)} />,
  );

  await expect(field.getByLabel('Logo', { exact: true })).toHaveAccessibleDescription(
    'Choose a file to replace it: PNG, JPEG, WebP or GIF, up to 10 MB.',
  );
  const image = field.getByRole('img', { name: 'Deck logo' });
  await expect(image).toHaveAttribute('src', currentLogo.url);
  await expect.poll(() => image.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBe(120);

  const remove = field.getByRole('button', { name: 'Remove logo' });
  await remove.focus();
  await page.keyboard.press('Enter');
  await expect.poll(() => changes).toEqual([null]);
});

test('a file of the wrong type shows an error and sends nothing', async ({ mount, page }) => {
  const log = await stubUpload(page);
  const field = await mount(
    <LogoField deckId={deckId} logo={null} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await field.getByLabel('Logo', { exact: true }).setInputFiles(svgFile);

  const error = field.getByRole('alert');
  await expect(error).toHaveText('Choose a PNG, JPEG, WebP or GIF image.');
  await expect(field.getByLabel('Logo', { exact: true })).toHaveAttribute('aria-invalid', 'true');
  expect(log).toEqual([]);
});

test('a failed upload PUT shows the server error and keeps the current logo', async ({
  mount,
  page,
}) => {
  await stubLogoImage(page);
  const log = await stubUpload(page, {
    status: 413,
    json: { error: 'The image is over 10 MB' },
  });
  const field = await mount(
    <LogoField deckId={deckId} logo={currentLogo} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect(field.getByRole('alert')).toHaveText('The image is over 10 MB');
  expect(log.filter((entry) => entry.startsWith('onChange'))).toEqual([]);
  expect(log).toHaveLength(2);
  await expect(field.getByTestId('logo-field-image')).toHaveAttribute('src', currentLogo.url);
  await expect(field.getByTestId('logo-field-file')).toBeEnabled();
});

test('a refused image POST shows the server error and puts nothing', async ({ mount, page }) => {
  const log: string[] = [];
  await page.route(imagesPath, (route) => {
    log.push(route.request().method());
    return route.fulfill({ status: 404, json: { error: 'Deck not found' } });
  });
  await page.route(uploadPath, (route) => {
    log.push('upload');
    return route.abort();
  });
  const field = await mount(
    <LogoField deckId={deckId} logo={null} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect(field.getByRole('alert')).toHaveText('Deck not found');
  expect(log).toEqual(['POST']);
});

test('a network failure on the upload says SlideWorks is unreachable and keeps the current logo', async ({
  mount,
  page,
}) => {
  await stubLogoImage(page);
  const log: string[] = [];
  await page.route(imagesPath, (route) => route.fulfill({ status: 201, json: imageUploadResult }));
  await page.route(uploadPath, (route) => {
    log.push('upload');
    return route.abort('internetdisconnected');
  });
  const field = await mount(
    <LogoField deckId={deckId} logo={currentLogo} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect(field.getByRole('alert')).toHaveText("Can't reach SlideWorks.");
  expect(log).toEqual(['upload']);
  await expect(field.getByTestId('logo-field-image')).toHaveAttribute('src', currentLogo.url);
  await expect(field.getByTestId('logo-field-file')).toBeEnabled();
});

test('an upload URL outside /api/uploads/ is refused and gets no bytes', async ({
  mount,
  page,
}) => {
  const log: string[] = [];
  await page.route(imagesPath, (route) =>
    route.fulfill({
      status: 201,
      json: { ...imageUploadResult, uploadUrl: 'https://elsewhere.example/api/decks/x/look' },
    }),
  );
  await page.route('**/api/decks/x/**', (route) => {
    log.push(route.request().method());
    return route.abort();
  });
  const field = await mount(
    <LogoField deckId={deckId} logo={null} onChange={(id) => log.push(`onChange ${id}`)} />,
  );

  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect(field.getByRole('alert')).toHaveText(
    "The upload didn't start: SlideWorks gave an upload address outside /api/uploads/.",
  );
  expect(log).toEqual([]);
});

test('the busy status is mounted empty, then says it is uploading', async ({ mount, page }) => {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route(imagesPath, (route) => route.fulfill({ status: 201, json: imageUploadResult }));
  await page.route(uploadPath, async (route) => {
    await held;
    return route.fulfill({ status: 204 });
  });
  const field = await mount(<LogoField deckId={deckId} logo={null} onChange={() => {}} />);

  const status = field.getByRole('status');
  await expect(status).toHaveText('');
  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);
  await expect(status).toHaveText('Uploading…');
  release();
  await expect(status).toHaveText('');
});
