// Every call the Templates area makes (T9), typed from the samples under src/contracts/screens.
// Archive, use and approve answer 204. A refused action is a 400 (409 for stale_revision and
// layout_changed) whose code is one of src/contracts/template-errors.json: templateErrorCode
// hands it to the caller.
import { ApiError, api } from '../api/client';
import type {
  ApproveLayoutRequest,
  CreateTemplateRequest,
  DecksUpdated,
  LayoutReviewView,
  TemplateChecklistView,
  TemplateCreated,
  TemplateEditorView,
  TemplateManifest,
  TemplatePublicView,
  TemplatePublished,
  TemplateSharingUpdate,
  TemplateSharingView,
  TemplatesView,
} from './types';

const root = '/api/templates';
const of = (id: string) => `${root}/${encodeURIComponent(id)}`;
const layoutOf = (id: string, name: string) => `${of(id)}/layouts/${encodeURIComponent(name)}`;

// An object, not loose functions, so that `templatesApi.useTemplate(id)` in a click handler does
// not read as a React hook to the hooks lint rule.
export const templatesApi = {
  list: () => api.get<TemplatesView>(root),
  create: (request: CreateTemplateRequest) => api.post<TemplateCreated>(root, request),
  get: (id: string) => api.get<TemplateEditorView>(of(id)),
  // The whole manifest and the revision the caller read; a newer revision on the server is 409 stale_revision.
  save: (id: string, manifest: TemplateManifest, revision: number) =>
    api.put<TemplateEditorView>(of(id), { manifest, revision }),
  checklist: (id: string) => api.get<TemplateChecklistView>(`${of(id)}/checklist`),
  publish: (id: string) => api.post<TemplatePublished>(`${of(id)}/publish`),
  archive: (id: string) => api.post<void>(`${of(id)}/archive`),
  updateMyDecks: (id: string) => api.post<DecksUpdated>(`${of(id)}/update-my-decks`),
  sharing: (id: string) => api.get<TemplateSharingView>(`${of(id)}/sharing`),
  setSharing: (id: string, update: TemplateSharingUpdate) =>
    api.put<TemplateSharingView>(`${of(id)}/sharing`, update),
  publicPage: (id: string) => api.get<TemplatePublicView>(`${of(id)}/public`),
  // "Use this template": adds a template shared with the caller to their own list.
  useTemplate: (id: string) => api.post<void>(`${of(id)}/use`),
  layoutReview: (id: string, name: string) =>
    api.get<LayoutReviewView>(`${layoutOf(id, name)}/review`),
  // Can be refused with layout_changed (409), layout_overflows or layout_low_contrast (400).
  approveLayout: (id: string, name: string, request: ApproveLayoutRequest) =>
    api.post<void>(`${layoutOf(id, name)}/approve`, request),
};

// The code of a refused template action (stale_revision, layout_changed, too_many_fonts, …), or
// null when the failure is not a refusal: a 404, a dropped connection.
export function templateErrorCode(error: unknown): string | null {
  return error instanceof ApiError && (error.status === 400 || error.status === 409)
    ? error.message
    : null;
}

// What to show a person when a template call fails.
// TODO: a refusal's body also carries a `message` in plain words and a `path`, which the shared
// fetch wrapper (api/client.ts) drops, so a refusal shows its code here. Show the body's message
// once ApiError carries it.
export function templateErrorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "Can't reach SlideWorks.";
}
