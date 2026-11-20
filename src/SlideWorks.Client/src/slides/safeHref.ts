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
  // 7. What is left there is a host and at most a port, nothing that could
  //    end the attribute the link is drawn in.
  if (!isHostAndPort(authority)) return undefined;
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

const isDigit = (c: number) => c >= 0x30 && c <= 0x39;
const isLetter = (c: number) => (c >= 0x41 && c <= 0x5a) || (c >= 0x61 && c <= 0x7a);
const isHex = (c: number) => isDigit(c) || (c >= 0x41 && c <= 0x46) || (c >= 0x61 && c <= 0x66);
const DOT = 0x2e;
const HYPHEN = 0x2d;
const COLON = 0x3a;
const MAX_PORT_DIGITS = 5;

// Letters, digits, dots and hyphens that neither start nor end with a dot or
// a hyphen, or an IPv6 literal in square brackets; then at most a colon and a
// port of 1 to 5 digits. One pass over the text, no pattern.
function isHostAndPort(authority: string): boolean {
  let i = 0;
  if (authority.charAt(0) === '[') {
    i = 1;
    while (i < authority.length && authority.charAt(i) !== ']') {
      const c = authority.charCodeAt(i);
      if (!isHex(c) && c !== COLON && c !== DOT) return false;
      i++;
    }
    if (i === authority.length) return false;
    i++;
  } else {
    while (i < authority.length && authority.charCodeAt(i) !== COLON) {
      const c = authority.charCodeAt(i);
      if (!isLetter(c) && !isDigit(c) && c !== DOT && c !== HYPHEN) return false;
      i++;
    }
    if (i === 0) return false;
    const first = authority.charCodeAt(0);
    const last = authority.charCodeAt(i - 1);
    if (first === DOT || first === HYPHEN || last === DOT || last === HYPHEN) return false;
  }
  if (i === authority.length) return true;
  // What follows the host is a colon and the port.
  if (authority.charCodeAt(i) !== COLON) return false;
  const digits = authority.length - i - 1;
  if (digits < 1 || digits > MAX_PORT_DIGITS) return false;
  for (let j = i + 1; j < authority.length; j++) {
    if (!isDigit(authority.charCodeAt(j))) return false;
  }
  return true;
}
