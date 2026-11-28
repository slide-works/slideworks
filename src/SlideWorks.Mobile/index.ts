// Order matters (a test pins it): crash reports first, so an error in what follows is caught, then the polyfills the
// core needs, then the router, which loads every route.
import './src/crashReports';
import './src/polyfills';
import 'expo-router/entry';
