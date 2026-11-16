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

// The upload URL is on the server's public origin, so its answers carry CORS headers the browser checks.
const cors = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'PUT',
  'access-control-allow-headers': 'content-type',
};

// Answers POST /images and the upload PUT, recording each in one ordered log with what reached onChange.
async function stubUpload(page: Page, upload: Answer = { status: 204 }) {
  const log: string[] = [];
  await page.route(imagesPath, (route) => {
    const request = route.request();
    log.push(`${request.method()} images ${JSON.stringify(request.postDataJSON())}`);
    return route.fulfill({ status: 201, json: imageUploadResult });
  });
  await page.route(uploadPath, (route: Route) => {
    const request = route.request();
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers: cors });
    log.push(
      `${request.method()} ${new URL(request.url()).pathname} ${request.headers()['content-type']} ${Boolean(request.postDataBuffer()?.length)}`,
    );
    return upload.json === undefined
      ? route.fulfill({ status: upload.status, headers: cors })
      : route.fulfill({ status: upload.status, headers: cors, json: upload.json });
  });
  return log;
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

  await expect(field.getByTestId('logo-field-image')).toHaveCount(0);
  await expect(field.getByTestId('logo-field-remove')).toHaveCount(0);
  await field.getByLabel('Logo', { exact: true }).setInputFiles(logoFile);

  await expect.poll(() => log.length).toBe(3);
  expect(log).toEqual([
    `POST images ${JSON.stringify({ ...imageUploadRequest, name: 'sample-image.png' })}`,
    `PUT ${new URL(imageUploadResult.uploadUrl).pathname} image/png true`,
    `onChange ${imageUploadResult.image}`,
  ]);
  await expect(field.getByTestId('logo-field-error')).toHaveCount(0);
  await expect(field.getByTestId('logo-field-file')).toBeEnabled();
});

test('the current logo shows, and remove hands null to onChange', async ({ mount, page }) => {
  await stubLogoImage(page);
  const changes: (string | null)[] = [];
  const field = await mount(
    <LogoField deckId={deckId} logo={currentLogo} onChange={(id) => changes.push(id)} />,
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
