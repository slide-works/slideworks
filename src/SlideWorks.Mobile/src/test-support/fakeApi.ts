// fakeApi(table) replaces fetch so the real core client runs against a table of answers (contract K10). Keys are
// "METHOD /path" (no origin, no query). A value is the body of a 200, a { status, body?, headers? } reply, or a
// function of the request returning either. Unlisted requests answer 404 { "error": "Not found" }.

export type FakeRequest = {
  method: string;
  path: string;
  origin: string;
  query: Record<string, string>;
  headers: Record<string, string>;
  body: unknown;
};
export type FakeReply = { status: number; body?: unknown; headers?: Record<string, string> };
export type FakeAnswer = FakeReply | object | string | number | boolean | null;
export type FakeRoute = FakeAnswer | ((request: FakeRequest) => FakeAnswer);
export type FakeApiTable = Record<string, FakeRoute>;

export type FakeApi = {
  // Every request so far, in order.
  calls(): FakeRequest[];
  // The requests to one "METHOD /path".
  callsTo(key: string): FakeRequest[];
  // Adds or replaces routes.
  set(routes: FakeApiTable): void;
  // Makes this table the one fetch answers from (fakeApi does it when created; renderApp does it again).
  install(): void;
};

const realFetch = globalThis.fetch;

export function restoreFetch(): void {
  globalThis.fetch = realFetch;
}

const replyKeys = new Set(['status', 'body', 'headers']);

function isReply(value: unknown): value is FakeReply {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { status?: unknown }).status === 'number' &&
    Object.keys(value).every((key) => replyKeys.has(key))
  );
}

function headersObject(init: HeadersInit | undefined): Record<string, string> {
  if (!init) return {};
  if (Array.isArray(init)) return Object.fromEntries(init);
  if (typeof Headers !== 'undefined' && init instanceof Headers)
    return Object.fromEntries(init.entries());
  return { ...(init as Record<string, string>) };
}

// Just enough of Response for the core client: ok, status, headers.get, json.
function response(reply: FakeReply) {
  const headers = Object.fromEntries(
    Object.entries(reply.headers ?? {}).map(([name, value]) => [name.toLowerCase(), value]),
  );
  return {
    ok: reply.status >= 200 && reply.status < 300,
    status: reply.status,
    headers: { get: (name: string) => headers[name.toLowerCase()] ?? null },
    json: async () => {
      if (reply.body === undefined) throw new SyntaxError('Unexpected end of JSON input');
      return JSON.parse(JSON.stringify(reply.body));
    },
  };
}

export function fakeApi(table: FakeApiTable = {}): FakeApi {
  const routes: FakeApiTable = { ...table };
  const recorded: FakeRequest[] = [];

  const fetchFake = async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(String(input), 'http://relative.invalid');
    const request: FakeRequest = {
      method: (init.method ?? 'GET').toUpperCase(),
      path: url.pathname,
      origin: String(input).startsWith('/') ? '' : url.origin,
      query: Object.fromEntries(url.searchParams),
      headers: headersObject(init.headers),
      body: typeof init.body === 'string' ? JSON.parse(init.body) : undefined,
    };
    recorded.push(request);
    const route = routes[`${request.method} ${request.path}`];
    if (route === undefined) return response({ status: 404, body: { error: 'Not found' } });
    const answer = typeof route === 'function' ? route(request) : route;
    return response(isReply(answer) ? answer : { status: 200, body: answer });
  };

  const api: FakeApi = {
    calls: () => [...recorded],
    callsTo: (key) => recorded.filter((call) => `${call.method} ${call.path}` === key),
    set: (more) => void Object.assign(routes, more),
    install: () => {
      globalThis.fetch = fetchFake as unknown as typeof fetch;
    },
  };
  api.install();
  return api;
}
