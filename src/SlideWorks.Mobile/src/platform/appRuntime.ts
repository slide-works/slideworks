import NetInfo from '@react-native-community/netinfo';
import type { QueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import Storage from 'expo-sqlite/kv-store';
import { createContext, useContext, useSyncExternalStore } from 'react';
import { configurePlatform, type KeyValueStore, type Platform } from '@slideworks/core/platform';
import { createGuestStore, type GuestStore } from '../guest/guestStore';
import { guestIdReader, type GuestIdReader } from '../guest/guestIdReader';
import { links } from '../links';
import { createAppQueryClient, createQueryPersister } from '../queries/queryClient';
import {
  createSessionStore,
  type SessionSnapshot,
  type SessionStore,
  type SyncSecureStore,
} from '../session/sessionStore';
import { createAppPlatform, type Connectivity } from './appPlatform';

// Everything the app's screens share: the session, the guest ids, the query client and the platform the core uses.
// The root layout starts it once, when it mounts, so the first render already reads storage.

export type AppRuntime = {
  session: SessionStore;
  guests: GuestStore;
  guestId: GuestIdReader;
  queryClient: QueryClient;
  persister: ReturnType<typeof createQueryPersister>;
  platform: Platform;
};

export type AppRuntimeDeps = {
  secure: SyncSecureStore;
  kv: KeyValueStore;
  connectivity: Connectivity;
  now: () => number;
  queryClient: QueryClient;
  goHome: () => void;
};

// Builds the runtime and configures the core's platform with it.
export function createAppRuntime(deps: AppRuntimeDeps): AppRuntime {
  const session = createSessionStore({ secure: deps.secure, kv: deps.kv });
  const guests = createGuestStore({ kv: deps.kv, now: deps.now });
  const guestId = guestIdReader(guests);
  const platform = createAppPlatform({
    session,
    guestId,
    kv: deps.kv,
    connectivity: deps.connectivity,
    queryClient: deps.queryClient,
    goHome: deps.goHome,
  });
  configurePlatform(platform);
  return {
    session,
    guests,
    guestId,
    queryClient: deps.queryClient,
    persister: createQueryPersister(deps.kv),
    platform,
  };
}

const kvStore: KeyValueStore = {
  getItem: (key) => Storage.getItemSync(key),
  setItem: (key, value) => Storage.setItemSync(key, value),
  removeItem: (key) => void Storage.removeItemSync(key),
};

const secureStore: SyncSecureStore = {
  getItem: (key) => SecureStore.getItem(key),
  setItem: (key, value) => SecureStore.setItem(key, value),
  deleteItemAsync: (key) => SecureStore.deleteItemAsync(key),
};

function netInfoConnectivity(): Connectivity {
  let offline = false;
  NetInfo.addEventListener((state) => {
    offline = state.isConnected === false;
  });
  return {
    isOffline: () => offline,
    onOnline: (listener) =>
      NetInfo.addEventListener((state) => {
        if (state.isConnected) listener();
      }),
  };
}

// The runtime on the device's real storage, connectivity and router.
export function startAppRuntime(): AppRuntime {
  return createAppRuntime({
    secure: secureStore,
    kv: kvStore,
    connectivity: netInfoConnectivity(),
    now: () => Date.now(),
    queryClient: createAppQueryClient(),
    goHome: () => router.replace(links.home()),
  });
}

export const AppRuntimeContext = createContext<AppRuntime | null>(null);

export function useAppRuntime(): AppRuntime {
  const runtime = useContext(AppRuntimeContext);
  if (!runtime) throw new Error('useAppRuntime needs the root layout (or renderScreen) above it.');
  return runtime;
}

export function useSession(): SessionSnapshot {
  const { session } = useAppRuntime();
  return useSyncExternalStore(session.subscribe, session.snapshot);
}
