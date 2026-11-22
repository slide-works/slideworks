import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { importBundle } from './bundle';
import { ArchiveDialog } from './list/ArchiveDialog';
import { refusalText } from './list/refusalText';
import { TemplateTable } from './list/TemplateTable';
import { TemplatesEmpty } from './list/TemplatesEmpty';
import { NewTemplateDialog } from './NewTemplateDialog';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { TemplateRow } from './types';
import { useTemplates } from './useTemplates';

const BUNDLE_FAILED = 'Could not read that bundle.';

// /templates: the person's own, the shared and the built-in templates from one useTemplates, with
// "New template" (also from a built-in's "Start from this"), "Upload a bundle…" and Archive.
export function TemplatesPage() {
  const navigate = useNavigate();
  const { load, reload } = useTemplates();
  const view = load.kind === 'loaded' ? load.templates : null;

  const heading = useRef<HTMLHeadingElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const uploadButton = useRef<HTMLButtonElement>(null);
  const [newFrom, setNewFrom] = useState<{ builtIn: string | null } | null>(null);
  const [archiving, setArchiving] = useState<TemplateRow | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  // Set when focus is owed to the Upload button once it is enabled again.
  const refocusUpload = useRef(false);
  // Set when an archive dialog closed and its row's button may be gone.
  const archivedClosed = useRef(false);

  useEffect(() => {
    if (!uploading && refocusUpload.current) {
      refocusUpload.current = false;
      uploadButton.current?.focus();
    }
  }, [uploading]);

  useEffect(() => {
    if (archiving === null && archivedClosed.current) {
      archivedClosed.current = false;
      // ModalDialog gave focus back to the Archive button if it is still there; else the heading.
      if (document.activeElement === null || document.activeElement === document.body) {
        heading.current?.focus();
      }
    }
  }, [archiving]);

  const upload = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setUploading(true);
    setUploadError(null);
    try {
      const created = await importBundle(file);
      navigate(`/templates/${encodeURIComponent(created.id)}/edit`);
    } catch (failure) {
      setUploadError(refusalText(failure) ?? BUNDLE_FAILED);
      refocusUpload.current = true;
      setUploading(false);
    }
  };

  const archived = async () => {
    await reload();
    archivedClosed.current = true;
    setArchiving(null);
  };

  return (
    <section data-testid={TEMPLATES_TEST_IDS.page} className="grid gap-5">
      <header className="flex flex-wrap items-center gap-3">
        <h1 ref={heading} tabIndex={-1} className="text-lg font-bold tracking-tight text-ink">
          Templates
        </h1>
        {view && (
          <div className="ml-auto flex flex-wrap items-center gap-3">
            <p role="alert" className="text-meta text-bad">
              {uploadError}
            </p>
            <input
              ref={fileInput}
              type="file"
              accept=".zip,application/zip"
              className="hidden"
              aria-label="Template bundle"
              tabIndex={-1}
              onChange={upload}
            />
            <button
              ref={uploadButton}
              type="button"
              className="btn-ghost"
              disabled={uploading}
              onClick={() => fileInput.current?.click()}
            >
              {uploading ? 'Uploading…' : 'Upload a bundle…'}
            </button>
            <button
              type="button"
              data-testid={TEMPLATES_TEST_IDS.newTemplate}
              className="btn-primary py-1.5"
              aria-haspopup="dialog"
              onClick={() => setNewFrom({ builtIn: null })}
            >
              New template
            </button>
          </div>
        )}
      </header>

      {load.kind === 'failed' && (
        <div className="panel flex flex-wrap items-center gap-3 px-4 py-3">
          <p role="alert" data-testid={TEMPLATES_TEST_IDS.error} className="text-meta text-bad">
            {load.message}
          </p>
          <button type="button" className="btn-ghost" onClick={() => void reload()}>
            Retry
          </button>
        </div>
      )}

      {view && (
        <>
          {view.mine.length === 0 && <TemplatesEmpty onNew={() => setNewFrom({ builtIn: null })} />}
          <TemplateTable
            groups={[
              { kind: 'mine', label: 'Yours', rows: view.mine },
              { kind: 'shared', label: 'Shared with you', rows: view.shared },
              { kind: 'builtIn', label: 'Built-in', rows: view.builtIn },
            ]}
            onArchive={setArchiving}
            onStartFrom={(row) => setNewFrom({ builtIn: row.id })}
          />
        </>
      )}

      {view && newFrom && (
        <NewTemplateDialog
          builtIn={view.builtIn}
          initialBuiltIn={newFrom.builtIn}
          onClose={() => setNewFrom(null)}
        />
      )}
      {archiving && (
        <ArchiveDialog row={archiving} onCancel={() => setArchiving(null)} onArchived={archived} />
      )}
    </section>
  );
}
