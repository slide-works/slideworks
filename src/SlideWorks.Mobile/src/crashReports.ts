// The first import in index.ts, so crash reporting starts before anything else runs (a test pins the order).
//
// Crashes and unhandled errors only, JavaScript and native: no tracing, no replay, no logs, no personal data.
// The native SDK starts before React Native loads (the plugin's useNativeInit in app.json), so a launch crash in a
// native module is reported too. This is the JavaScript half.
//
// The DSN lives in sentry.options.json (public by design). An empty DSN means no reports, which is what a
// self-hosted fork gets. Development builds never report.
//
// Source maps: the EAS release build uploads them with SENTRY_AUTH_TOKEN, set as an EAS environment variable. A
// release build fails without the token. The upload also needs SENTRY_ORG and SENTRY_PROJECT (the plugin in app.json
// sets neither; the SDK reads these two). To build without uploading, set SENTRY_DISABLE_AUTO_UPLOAD=true.
//
// No breadcrumbs, JavaScript or native: a breadcrumb can hold a request URL, and the hub's carries access_token. The
// native SDK reads sentry.options.json itself (maxBreadcrumbs 0 and the automatic-breadcrumb, tracing and hang
// switches there; a test pins them), so the same values are not repeated for it here. beforeSend is the last defence.
import * as Sentry from '@sentry/react-native';
import options from '../sentry.options.json';

declare const __DEV__: boolean;

// Only what captures a crash or an unhandled error, plus what makes its stack readable. Everything else the SDK adds
// by default (breadcrumbs, which can hold request URLs; device, http and Expo contexts) is left out.
const CAPTURE_INTEGRATIONS = new Set([
  'ReactNativeErrorHandlers',
  'NativeLinkedErrors',
  'Dedupe',
  'InboundFilters',
  'FunctionToString',
  'DebugMeta',
  'RewriteFrames',
  'Release',
  'EventOrigin',
  'SdkInfo',
]);

export function keepCaptureIntegrations<T extends { name: string }>(defaults: T[]): T[] {
  return defaults.filter((i) => CAPTURE_INTEGRATIONS.has(i.name));
}

const QUERY_VALUE = /([?&][^=&#\s"'<>]+=)[^&#\s"'<>]*/g;

function scrub(value: unknown): unknown {
  if (typeof value === 'string') return value.replace(QUERY_VALUE, '$1[Filtered]');
  if (Array.isArray(value)) return value.map(scrub);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, scrub(v)]));
  }
  return value;
}

// No request, no user, and no query value in any string of the event (a URL in an error message or a stack frame).
export function scrubEvent<T extends object>(event: T): T {
  const rest = { ...event } as Record<string, unknown>;
  delete rest.request;
  delete rest.user;
  return scrub(rest) as T;
}

export function crashReportOptions(dsn: string) {
  return {
    dsn,
    sendDefaultPii: false,
    tracesSampleRate: 0,
    enableLogs: false,
    enableAutoSessionTracking: false,
    attachScreenshot: false,
    attachViewHierarchy: false,
    maxBreadcrumbs: 0,
    enableNetworkEventBreadcrumbs: false,
    enableCaptureFailedRequests: false,
    enableAutoPerformanceTracing: false,
    enableUserInteractionTracing: false,
    enableNativeFramesTracking: false,
    enableStallTracking: false,
    enableAppHangTracking: false,
    beforeBreadcrumb: () => null,
    beforeSend: scrubEvent,
    integrations: keepCaptureIntegrations,
  };
}

function startCrashReports(): void {
  if (!options.dsn.trim() || __DEV__) return;

  Sentry.init(crashReportOptions(options.dsn));
}

startCrashReports();
