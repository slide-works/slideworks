import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import { Stack } from 'expo-router';
import { useState, useSyncExternalStore } from 'react';
import { AppRuntimeContext, startAppRuntime } from '../src/platform/appRuntime';
import { PERSIST_MAX_AGE_MS } from '../src/queries/queryClient';

// The root: starts the app runtime (platform, stores, query client) before anything renders or fetches, and gates
// routes on the device session. Home and Join never need one; Sign in shows only when signed out.
export default function RootLayout() {
  const [runtime] = useState(startAppRuntime);
  const { signedIn } = useSyncExternalStore(runtime.session.subscribe, runtime.session.snapshot);

  return (
    <AppRuntimeContext.Provider value={runtime}>
      <PersistQueryClientProvider
        client={runtime.queryClient}
        persistOptions={{ persister: runtime.persister, maxAge: PERSIST_MAX_AGE_MS }}
      >
        <Stack>
          <Stack.Screen name="index" />
          <Stack.Screen name="join/[code]" />
          <Stack.Protected guard={!signedIn}>
            <Stack.Screen name="sign-in" />
          </Stack.Protected>
          <Stack.Protected guard={signedIn}>
            <Stack.Screen name="(signed-in)" options={{ headerShown: false }} />
          </Stack.Protected>
        </Stack>
      </PersistQueryClientProvider>
    </AppRuntimeContext.Provider>
  );
}
