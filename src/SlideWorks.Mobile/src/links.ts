import type { Href } from 'expo-router';

// The only place a route path is written: screens navigate with router.push(links.talk(id)).

// The hosted server, which join links on the hosted domains open (K8) and Sign in prefills (D3).
export const HOSTED_SERVER = 'https://slideworks.app';

export const links = {
  home: (): Href => '/',
  join: (code: string, server?: string): Href =>
    server === undefined
      ? `/join/${encodeURIComponent(code)}`
      : `/join/${encodeURIComponent(code)}?server=${encodeURIComponent(server)}`,
  signIn: (): Href => '/sign-in',
  talks: (): Href => '/talks',
  talk: (id: string): Href => `/talk/${encodeURIComponent(id)}`,
  account: (): Href => '/account',
};
