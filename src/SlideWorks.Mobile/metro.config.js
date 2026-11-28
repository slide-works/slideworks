// Expo's Metro config (it finds the pnpm workspace on its own), wrapped by the crash-report SDK so each bundle and
// source map get a debug id and the SDK reads sentry.options.json.
const { getSentryExpoConfig } = require('@sentry/react-native/metro');

module.exports = getSentryExpoConfig(__dirname);
