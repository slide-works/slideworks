// In-memory stand-ins for the native modules, installed by jest.setup.ts and reset before each test. Tests reach them
// through the harness (renderApp seeds storage) or directly to assert what was stored.

const secureValues = new Map<string, string>();
const kvValues = new Map<string, string>();

export const mockStorage = {
  secure: secureValues,
  kv: kvValues,
  reset() {
    secureValues.clear();
    kvValues.clear();
    webBrowser.reset();
  },
};

// expo-secure-store: the synchronous API plus the async ones the app may call.
export const secureStoreModule = {
  getItem: (key: string) => secureValues.get(key) ?? null,
  setItem: (key: string, value: string) => void secureValues.set(key, value),
  getItemAsync: async (key: string) => secureValues.get(key) ?? null,
  setItemAsync: async (key: string, value: string) => void secureValues.set(key, value),
  deleteItemAsync: async (key: string) => void secureValues.delete(key),
};

// expo-sqlite/kv-store's default Storage instance, synchronous methods only.
const kvStorage = {
  getItemSync: (key: string) => kvValues.get(key) ?? null,
  setItemSync: (key: string, value: string) => void kvValues.set(key, value),
  removeItemSync: (key: string) => kvValues.delete(key),
  getAllKeysSync: () => [...kvValues.keys()],
  clearSync: () => {
    kvValues.clear();
    return true;
  },
};
export const kvStoreModule = { __esModule: true, default: kvStorage, Storage: kvStorage };

type AuthSessionResult =
  { type: 'success'; url: string } | { type: 'cancel' | 'dismiss' | 'locked' };
let settle: ((result: AuthSessionResult) => void) | null = null;
let fail: ((error: Error) => void) | null = null;
const opened: { url: string; redirectUrl: string | null | undefined }[] = [];

// Drives expo-web-browser's openAuthSessionAsync: the promise a screen awaits settles only when a test completes or
// cancels the handoff.
export const webBrowser = {
  opened,
  completeAuthSession(url: string) {
    if (!settle) throw new Error('No auth session is open.');
    settle({ type: 'success', url });
    settle = null;
  },
  // Ends the session with a non-success result: 'dismiss' and 'cancel' are the person closing the browser.
  endAuthSession(type: 'cancel' | 'dismiss' | 'locked') {
    if (!settle) throw new Error('No auth session is open.');
    settle({ type });
    settle = null;
  },
  // Makes the openAuthSessionAsync promise reject, as a native failure would.
  failAuthSession(error: Error) {
    if (!fail) throw new Error('No auth session is open.');
    fail(error);
    fail = null;
    settle = null;
  },
  cancelAuthSession() {
    if (!settle) throw new Error('No auth session is open.');
    settle({ type: 'cancel' });
    settle = null;
  },
  reset() {
    opened.length = 0;
    settle = null;
    fail = null;
  },
};

export const webBrowserModule = {
  openAuthSessionAsync: (url: string, redirectUrl?: string | null) => {
    opened.push({ url, redirectUrl });
    return new Promise<AuthSessionResult>((resolve, reject) => {
      settle = resolve;
      fail = reject;
    });
  },
  openBrowserAsync: async () => ({ type: 'opened' }),
  dismissAuthSession: () => undefined,
  dismissBrowser: async () => ({ type: 'dismiss' }),
  maybeCompleteAuthSession: () => ({ type: 'failed', message: 'Not on web.' }),
  warmUpAsync: async () => ({}),
  coolDownAsync: async () => ({}),
};

export const APP_VERSION = '0.0.0';
export const APP_BUILD = '1';
export const DEVICE_NAME = 'Test Phone';
