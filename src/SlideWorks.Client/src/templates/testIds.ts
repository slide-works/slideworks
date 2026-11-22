import type { EditorTab } from './types';

// Every data-testid of the Templates area, so specs and page objects never spell one by hand.
export const TEMPLATES_TEST_IDS = {
  page: 'templates-page',
  error: 'templates-error',
  row: 'template-row',
  rowName: 'template-name',
  newDialog: 'new-template-dialog',
  shareDialog: 'template-share-dialog',
  publicPage: 'template-public-page',
  layoutReviewPage: 'layout-review-page',
  editor: 'template-editor',
  editorError: 'template-editor-error',
  editorSaveError: 'template-editor-save-error',
  editorTabs: 'template-editor-tabs',
  editorPanel: 'template-editor-panel',
  preview: 'template-editor-preview',
  checklist: 'template-publish-checklist',
  newTemplate: 'new-template',
  newName: 'new-template-name',
  newFrom: 'new-template-from',
  newCreate: 'new-template-create',
  // The prefix of one table group's id: template-group-mine, -shared, -builtIn.
  group: 'template-group',
  rowShortName: 'template-short-name',
  rowStatus: 'template-status',
  fontRights: 'font-rights-tick',
  fontAddFiles: 'font-add-files',
  fontFileInput: 'font-file-input',
  fontToAdd: 'font-to-add',
  fontRole: 'font-to-add-role',
  fontType: 'font-type',
  fontFamily: 'font-to-add-family',
  fontWeight: 'font-to-add-weight',
  fontAdd: 'font-to-add-add',
  publish: 'template-publish',
} as const;

// One tab button of the editor's tab bar, and the tab's own content.
export const editorTabButtonTestId = (tab: EditorTab) => `template-editor-tab-${tab}`;
export const editorTabTestId = (tab: EditorTab) => `template-tab-${tab}`;
