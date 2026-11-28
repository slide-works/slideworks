import { HOSTED_SERVER, links } from '../src/links';

// Maps the URLs the system hands the app to routes (contract K8, decision D16). Join links on the hosted domains open
// that talk on the hosted server; slideworks://join keeps the server it names; slideworks://signed-in is swallowed,
// because on Android the sign-in redirect also arrives as an ordinary deep link, which would show Unmatched route.
const hostedJoin = /^https:\/\/(?:slideworks\.app|slideworks\.live)\/j\/([^/?#]+)\/?(?:[?#].*)?$/i;
const joinHostCode = /^https:\/\/slideworks\.live\/(\d{6})\/?(?:[?#].*)?$/i;
const schemeJoin = /^slideworks:\/\/join\/([^/?#]+)\/?(?:\?([^#]*))?/i;
const schemeSignedIn = /^slideworks:\/\/signed-in(?:[/?#].*)?$/i;

export function redirectSystemPath({
  path,
  initial,
}: {
  path: string | null;
  initial: boolean;
}): string | null {
  if (path === null) return path;
  if (schemeSignedIn.test(path)) {
    // The browser session already delivered it; stay where the app is (Home when it is just starting).
    return initial ? String(links.home()) : null;
  }
  const hosted = hostedJoin.exec(path) ?? joinHostCode.exec(path);
  if (hosted) return String(links.join(decodeURIComponent(hosted[1]), HOSTED_SERVER));
  const scheme = schemeJoin.exec(path);
  if (scheme) {
    const server = new URLSearchParams(scheme[2] ?? '').get('server') ?? undefined;
    return String(links.join(decodeURIComponent(scheme[1]), server));
  }
  return path;
}
