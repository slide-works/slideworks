import { beforeEach, describe, expect, it, vi } from 'vitest';

const init = vi.fn();
vi.mock('@sentry/react-native', () => ({ init }));

const DSN = 'https://publickey@o0.ingest.example/1';

async function load(dsn: string, dev: boolean) {
  vi.resetModules();
  vi.doMock('../sentry.options.json', () => ({ default: { dsn } }));
  vi.stubGlobal('__DEV__', dev);
  await import('./crashReports');
}

describe('crashReports', () => {
  beforeEach(() => init.mockClear());

  it('empty DSN never initialises', async () => {
    await load('', false);
    await load('   ', false);

    expect(init).not.toHaveBeenCalled();
  });

  it('development build never initialises', async () => {
    await load(DSN, true);

    expect(init).not.toHaveBeenCalled();
  });

  it('release with DSN initialises with no tracing, replay or PII', async () => {
    await load(DSN, false);

    expect(init).toHaveBeenCalledTimes(1);
    const config = init.mock.calls[0][0];
    expect(config).toMatchObject({
      dsn: DSN,
      sendDefaultPii: false,
      tracesSampleRate: 0,
      enableLogs: false,
    });
    expect(config).not.toHaveProperty('replaysSessionSampleRate');
    expect(config).not.toHaveProperty('replaysOnErrorSampleRate');
  });

  it('release keeps only crash and unhandled-error integrations', async () => {
    await load(DSN, false);

    const names = [
      'ReactNativeErrorHandlers',
      'Breadcrumbs',
      'HttpContext',
      'DeviceContext',
      'MobileReplay',
      'ReactNativeTracing',
      'Dedupe',
    ];
    const kept = init.mock.calls[0][0].integrations(names.map((name) => ({ name })));

    expect(kept.map((i: { name: string }) => i.name)).toEqual([
      'ReactNativeErrorHandlers',
      'Dedupe',
    ]);
  });
});
