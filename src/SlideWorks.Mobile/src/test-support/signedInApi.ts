import { aDecksLibrary, aMe } from './builders';
import { fakeApi, type FakeApiTable } from './fakeApi';

// The table a signed-in app reads; spread overrides on top.
export function signedInApi(overrides: FakeApiTable = {}) {
  return fakeApi({ 'GET /api/me': aMe(), 'GET /api/decks': aDecksLibrary(), ...overrides });
}
