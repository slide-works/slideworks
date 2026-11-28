import nativeOptions from '../sentry.options.json';
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

  it('release turns every breadcrumb source off and scrubs events', async () => {
    await load(DSN, false);

    const config = init.mock.calls[0][0];
    expect(config).toMatchObject({ maxBreadcrumbs: 0, enableNetworkEventBreadcrumbs: false });
    expect(config.beforeBreadcrumb({ message: 'GET /hubs?access_token=swd_x' })).toBeNull();

    const url = 'https://x.example/hubs/deck?access_token=swd_secret&guest=g1';
    const event = config.beforeSend({
      request: { url },
      user: { id: '1' },
      message: `failed ${url}`,
      exception: {
        values: [{ value: `boom ${url}`, stacktrace: { frames: [{ filename: url }] } }],
      },
    });
    expect(event).not.toHaveProperty('request');
    expect(event).not.toHaveProperty('user');
    expect(JSON.stringify(event)).not.toContain('swd_secret');
    expect(JSON.stringify(event)).not.toContain('g1');
    expect(event.message).toBe(
      'failed https://x.example/hubs/deck?access_token=[Filtered]&guest=[Filtered]',
    );
  });

  it('native options file turns off breadcrumbs, tracing and attachments', () => {
    expect(nativeOptions).toMatchObject({
      dsn: '',
      sendDefaultPii: false,
      tracesSampleRate: 0,
      maxBreadcrumbs: 0,
      enableAutoSessionTracking: false,
      attachScreenshot: false,
      attachViewHierarchy: false,
      enableAppLifecycleBreadcrumbs: false,
      enableActivityLifecycleBreadcrumbs: false,
      enableAppComponentBreadcrumbs: false,
      enableSystemEventBreadcrumbs: false,
      enableNetworkEventBreadcrumbs: false,
      enableCaptureFailedRequests: false,
      enableAutoPerformanceTracing: false,
      enableUserInteractionTracing: false,
      enableNativeFramesTracking: false,
      enableAppHangTracking: false,
      enableWatchdogTerminationTracking: false,
    });
  });
});
