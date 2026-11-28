import { getDefaultIntegrations } from '@sentry/react-native/dist/js/integrations/default';
import { keepCaptureIntegrations } from './crashReports';

// jest.setup.ts mocks '@sentry/react-native'; the default integrations are read from the installed SDK itself.
describe('crashReports integrations', () => {
  it('drops the SDK real default breadcrumb and context integrations, keeps the error handlers', () => {
    const defaults = getDefaultIntegrations({
      dsn: 'https://k@o0.ingest.example/1',
      enableNative: true,
    } as never);
    const names = defaults.map((i) => i.name);
    for (const present of ['Breadcrumbs', 'ReactNativeErrorHandlers', 'HttpContext'])
      expect(names).toContain(present);

    const kept = keepCaptureIntegrations(defaults).map((i) => i.name);

    expect(kept).toContain('ReactNativeErrorHandlers');
    expect(kept).not.toContain('Breadcrumbs');
    expect(kept).not.toContain('HttpContext');
  });
});
