import type { Ref } from 'react';

export type VersionUpdate =
  | { kind: 'idle' }
  | { kind: 'running' }
  | { kind: 'failed'; message: string }
  | { kind: 'done'; version: number };

export type VersionOfferProps = {
  // The template's name, as the person knows it. Drawn as text only.
  templateLabel: string;
  version: number;
  latest: number;
  update: VersionUpdate;
  buttonRef: Ref<HTMLButtonElement>;
  onUpdate(): void;
};

// A deck pinned to an older published version of an uploaded template is offered the newer one. The
// caller draws it for the deck's owner only, and only while latest is greater than version.
export function VersionOffer({
  templateLabel,
  version,
  latest,
  update,
  buttonRef,
  onUpdate,
}: VersionOfferProps) {
  const running = update.kind === 'running';
  return (
    <div
      data-testid="look-version-offer"
      role="group"
      aria-label={`${templateLabel} has a newer version`}
      className="grid gap-2 rounded-md border border-edge bg-panel-2 p-3 text-meta"
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold text-ink">
          {templateLabel} · version {version}
        </span>
        <span className="rounded-full bg-accent px-2 py-0.5 text-xs font-semibold text-white">
          version {latest} available
        </span>
      </div>
      <div>
        <button
          ref={buttonRef}
          type="button"
          data-testid="look-version-update"
          className="btn-primary py-1.5"
          disabled={running}
          aria-label={
            running ? undefined : `Update this deck to version ${latest} of ${templateLabel}`
          }
          onClick={onUpdate}
        >
          {running ? 'Updating…' : 'Update this deck'}
        </button>
      </div>
      {update.kind === 'failed' && (
        <p role="alert" data-testid="look-version-error" className="text-bad">
          {update.message}
        </p>
      )}
    </div>
  );
}

export type VersionUpdatedProps = { version: number; messageRef: Ref<HTMLParagraphElement> };

// What the panel says once the deck is on the newer version. It is focusable so focus can land on it.
export function VersionUpdated({ version, messageRef }: VersionUpdatedProps) {
  return (
    <p
      ref={messageRef}
      role="status"
      tabIndex={-1}
      data-testid="look-version-message"
      className="text-meta text-ok"
    >
      Now on version {version}.
    </p>
  );
}
