import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { importBundle } from './bundle';

const file = new File(['PK'], 'kit.zip', { type: 'application/zip' });

function answer(status: number, body: unknown): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(body), { status })),
  );
}

async function failure(): Promise<ApiError> {
  try {
    await importBundle(file);
  } catch (error) {
    if (error instanceof ApiError) return error;
  }
  throw new Error('The import did not fail with an ApiError.');
}

describe('importBundle', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('keeps the errors list of a refused file', async () => {
    const errors = [{ path: 'fonts[0].file', code: 'font_not_woff2', message: 'Not woff2.' }];
    answer(400, {
      error: 'font_not_woff2',
      message: 'Fonts must be woff2.',
      path: 'a.woff2',
      errors,
    });

    const error = await failure();

    expect(error.status).toBe(400);
    expect(error.code).toBe('font_not_woff2');
    expect(error.detail).toBe('Fonts must be woff2.');
    expect(error.path).toBe('a.woff2');
    expect(error.errors).toEqual(errors);
  });

  it('drops a finding that is not path, code and message', async () => {
    answer(400, { error: 'x', errors: [{ path: 1 }, 'text'] });

    expect((await failure()).errors).toEqual([]);
  });

  it('answers a 401 with the sign-in sentence', async () => {
    answer(401, { error: 'Unauthorized' });

    expect((await failure()).detail).toBe(
      'Your session has ended. Sign in again, then upload the bundle.',
    );
  });

  it('answers a pending 403 with the waiting sentence', async () => {
    answer(403, { error: 'pending_approval' });

    expect((await failure()).detail).toBe('Your request is waiting for an admin to approve it.');
  });

  it('resolves with the new id on 201', async () => {
    answer(201, { id: 'tpl_new' });

    await expect(importBundle(file)).resolves.toEqual({ id: 'tpl_new' });
  });
});
