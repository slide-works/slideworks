import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AppRuntimeContext, createAppRuntime } from '../platform/appRuntime';
import { secureStoreModule, kvStoreModule } from './nativeMocks';

// Renders one screen on a 390×844 notched phone, light or dark, with its own query cache (retry: false) and an app
// runtime over the in-memory storage. Await it (React Native Testing Library 14's render is async).
export const PHONE_METRICS = {
  frame: { x: 0, y: 0, width: 390, height: 844 },
  insets: { top: 47, left: 0, right: 0, bottom: 34 },
};

const useColorScheme: jest.Mock = jest.requireMock(
  'react-native/Libraries/Utilities/useColorScheme',
).default;

export function testQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
}

export async function renderScreen(
  ui: ReactElement,
  { scheme = 'light' }: { scheme?: 'light' | 'dark' } = {},
) {
  useColorScheme.mockReturnValue(scheme);
  const queryClient = testQueryClient();
  const runtime = createAppRuntime({
    secure: secureStoreModule,
    kv: {
      getItem: kvStoreModule.default.getItemSync,
      setItem: kvStoreModule.default.setItemSync,
      removeItem: (key) => void kvStoreModule.default.removeItemSync(key),
    },
    connectivity: { isOffline: () => false, onOnline: () => () => undefined },
    now: () => Date.now(),
    queryClient,
    goHome: () => undefined,
  });
  const result = await render(
    <SafeAreaProvider initialMetrics={PHONE_METRICS}>
      <AppRuntimeContext.Provider value={runtime}>
        <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
      </AppRuntimeContext.Provider>
    </SafeAreaProvider>,
  );
  return { ...result, runtime, queryClient };
}
