// The one place an address from a slot becomes an href (K12). An agent wrote
// the text, so it is checked as the raw string before any URL parser sees it:
// the browser's parser and the server's disagree on odd inputs (a tab inside
// the scheme, a backslash for a slash, user info, a percent sign in the host),
// and these rules leave only strings both read the same way. The server runs
// the same rules; src/contracts/link-cases.json holds the cases for both.
//
// A link is never trimmed or rewritten: the answer is the text as it was given,
// or undefined, and a caller draws a refused value as plain text. Set the
// answer as the href attribute of an element; never build HTML text from it.

const MAX_LENGTH = 500;
const SCHEMES = ['http://', 'https://'];
// What may not follow the //: a third slash, a backslash, or an empty host
// before user info or a port.
const NOT_A_HOST_START = ['/', '\\', '@', ':'];

export function safeHref(value: string | undefined): string | undefined {
  // The text comes from stored JSON, so its type is checked as well.
  if (typeof value !== 'string') return undefined;
  // 1. The length first, so the size of the text never sets the cost of the rest.
  if (value.length > MAX_LENGTH) return undefined;
  // 2. Printable ASCII only, U+0021 to U+007E: no whitespace, no control
  //    character, nothing outside ASCII.
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code < 0x21 || code > 0x7e) return undefined;
  }
  // 3. No backslash anywhere.
  if (value.includes('\\')) return undefined;
  // 4. http:// or https:// in any case, then the first character of a host.
  const start = value.slice(0, 8).toLowerCase();
  const scheme = SCHEMES.find((s) => start.startsWith(s));
  if (scheme === undefined) return undefined;
  if (value.length === scheme.length) return undefined;
  if (NOT_A_HOST_START.includes(value.charAt(scheme.length))) return undefined;
  // 5 and 6. No user info and no percent sign before the path starts.
  const authority = value.slice(scheme.length).split(/[/?#]/, 1)[0];
  if (authority.includes('@') || authority.includes('%')) return undefined;
  // The parser has the last word: an http or https address with a host and no
  // user name or password.
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return undefined;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return undefined;
  if (url.hostname === '' || url.username !== '' || url.password !== '') return undefined;
  return value;
}
