import { getRandomValues, randomUUID } from 'expo-crypto';

// What @slideworks/core uses at call time that React Native lacks. fetch, URL and URLSearchParams (Expo's standard
// URL) and TextEncoder (Hermes) are already global; crypto is not.
type CryptoGlobal = { crypto?: Partial<Pick<Crypto, 'getRandomValues' | 'randomUUID'>> };

const host = globalThis as CryptoGlobal;
host.crypto ??= {};
host.crypto.getRandomValues ??= getRandomValues as Crypto['getRandomValues'];
host.crypto.randomUUID ??= randomUUID as Crypto['randomUUID'];
