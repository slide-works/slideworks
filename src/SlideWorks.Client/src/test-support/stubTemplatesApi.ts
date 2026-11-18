import type { Page } from 'playwright/test';
import {
  layoutReview,
  templateChecklist,
  templateEditor,
  templatePublic,
  templateSharing,
  templates,
} from './fixtures/templates';

type StubbedResponse = { status?: number; json?: unknown } | 'network-down';

// Every call of src/templates/api.ts, by the name it has on templatesApi.
export type TemplateCall =
  | 'list'
  | 'create'
  | 'get'
  | 'save'
  | 'checklist'
  | 'publish'
  | 'archive'
  | 'updateMyDecks'
  | 'sharing'
  | 'setSharing'
  | 'publicPage'
  | 'useTemplate'
  | 'layoutReview'
  | 'approveLayout';

// One request the page made: which call, the template it was for, and the body it sent.
export type TemplateRequest = { call: TemplateCall; id: string | null; body: unknown };

const ONE = '([^/]+)';
const CALLS: readonly { call: TemplateCall; method: string; path: RegExp }[] = [
  { call: 'list', method: 'GET', path: /^\/api\/templates$/ },
  { call: 'create', method: 'POST', path: /^\/api\/templates$/ },
  { call: 'get', method: 'GET', path: new RegExp(`^/api/templates/${ONE}$`) },
  { call: 'save', method: 'PUT', path: new RegExp(`^/api/templates/${ONE}$`) },
  { call: 'checklist', method: 'GET', path: new RegExp(`^/api/templates/${ONE}/checklist$`) },
  { call: 'publish', method: 'POST', path: new RegExp(`^/api/templates/${ONE}/publish$`) },
  { call: 'archive', method: 'POST', path: new RegExp(`^/api/templates/${ONE}/archive$`) },
  {
    call: 'updateMyDecks',
    method: 'POST',
    path: new RegExp(`^/api/templates/${ONE}/update-my-decks$`),
  },
  { call: 'sharing', method: 'GET', path: new RegExp(`^/api/templates/${ONE}/sharing$`) },
  { call: 'setSharing', method: 'PUT', path: new RegExp(`^/api/templates/${ONE}/sharing$`) },
  { call: 'publicPage', method: 'GET', path: new RegExp(`^/api/templates/${ONE}/public$`) },
  { call: 'useTemplate', method: 'POST', path: new RegExp(`^/api/templates/${ONE}/use$`) },
  {
    call: 'layoutReview',
    method: 'GET',
    path: new RegExp(`^/api/templates/${ONE}/layouts/[^/]+/review$`),
  },
  {
    call: 'approveLayout',
    method: 'POST',
    path: new RegExp(`^/api/templates/${ONE}/layouts/[^/]+/approve$`),
  },
];

// What each call answers unless a spec says otherwise: the screen samples, in the status the
// server uses (201 for a new draft, 204 with no body for archive, use and approve).
const DEFAULTS: { [C in TemplateCall]: { status: number; json?: unknown } } = {
  list: { status: 200, json: templates },
  create: { status: 201, json: { id: templateEditor.id } },
  get: { status: 200, json: templateEditor },
  save: { status: 200, json: templateEditor },
  checklist: { status: 200, json: templateChecklist },
  publish: { status: 200, json: { version: templateEditor.version + 1, decksBehind: 3 } },
  archive: { status: 204 },
  updateMyDecks: { status: 200, json: { count: 3 } },
  sharing: { status: 200, json: templateSharing },
  setSharing: { status: 200, json: templateSharing },
  publicPage: { status: 200, json: templatePublic },
  useTemplate: { status: 204 },
  layoutReview: { status: 200, json: layoutReview },
  approveLayout: { status: 204 },
};

// Answers every Templates call at the network boundary in component tests, so the page under
// test makes its real requests. Each call answers its sample unless `responses` names it; a
// refusal is `{ status: 400, json: { error: code, message: text } }`. The returned list fills
// with the requests the page made, in order.
export async function stubTemplatesApi(
  page: Page,
  responses: { [C in TemplateCall]?: StubbedResponse } = {},
): Promise<TemplateRequest[]> {
  const requests: TemplateRequest[] = [];
  await page.route(/\/api\/templates(\/|\?|$)/, (route) => {
    const request = route.request();
    const { pathname } = new URL(request.url());
    const found = CALLS.find(
      ({ method, path }) => method === request.method() && path.test(pathname),
    );
    if (!found) return route.fallback();

    const id = found.path.exec(pathname)?.[1];
    requests.push({
      call: found.call,
      id: id === undefined ? null : decodeURIComponent(id),
      body: request.postDataJSON() as unknown,
    });

    const response = responses[found.call] ?? DEFAULTS[found.call];
    if (response === 'network-down') return route.abort('connectionrefused');
    const status = response.status ?? 200;
    return response.json === undefined
      ? route.fulfill({ status })
      : route.fulfill({ status, json: response.json });
  });
  return requests;
}
