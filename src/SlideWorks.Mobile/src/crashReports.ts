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
// release build fails without the token. To build without uploading, set SENTRY_DISABLE_AUTO_UPLOAD=true.
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

function startCrashReports(): void {
  if (!options.dsn.trim() || __DEV__) return;

  Sentry.init({
    dsn: options.dsn,
    sendDefaultPii: false,
    tracesSampleRate: 0,
    enableLogs: false,
    enableAutoSessionTracking: false,
    attachScreenshot: false,
    attachViewHierarchy: false,
    integrations: (defaults) => defaults.filter((i) => CAPTURE_INTEGRATIONS.has(i.name)),
  });
}

startCrashReports();
