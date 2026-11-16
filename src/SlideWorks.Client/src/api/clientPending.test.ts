import { afterEach, describe, expect, it, vi } from 'vitest';
import { stubFetch } from '../test-support/stubFetch';
import { ApiError, api, setOnPendingApproval } from './client';

describe('api client pending approval handling', () => {
  afterEach(() => {
    setOnPendingApproval(() => undefined);
    vi.unstubAllGlobals();
  });

  it('pending_approval 403 calls the pending handler', async () => {
    stubFetch(403, { error: 'pending_approval' });
    const onPending = vi.fn();
    setOnPendingApproval(onPending);

    const error = await api.get('/api/decks').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(403);
    expect(onPending).toHaveBeenCalledOnce();
  });

  it('other 403 does not', async () => {
    stubFetch(403, { error: 'Admins only' });
    const onPending = vi.fn();
    setOnPendingApproval(onPending);

    const error = await api.get('/api/admin/users').catch((e: unknown) => e);

    expect((error as ApiError).status).toBe(403);
    expect(onPending).not.toHaveBeenCalled();
  });

  it('403 on /api/me does not', async () => {
    stubFetch(403, { error: 'pending_approval' });
    const onPending = vi.fn();
    setOnPendingApproval(onPending);

    const error = await api.get('/api/me').catch((e: unknown) => e);

    expect((error as ApiError).status).toBe(403);
    expect(onPending).not.toHaveBeenCalled();
  });
});
