import { useId, useRef, useState } from 'react';
import { editorTabTestId } from '../../testIds';
import type { TemplateManifest } from '../../types';
import type { ChangeOutcome } from '../../useTemplateDraft';
import { omit } from '../basics/fields';
import { LogoSlot, type LogoSide } from '../logoart/LogoSlot';
import { OrnamentDots } from '../logoart/OrnamentDots';
import { TitleArtChoice, type ArtChoice } from '../logoart/TitleArtChoice';
import type { EditorTabProps } from '../tabProps';
import { uploadAsset } from '../uploadAsset';

type Control = LogoSide | 'showOnDark' | 'art' | 'dots';

// The control a refusal's manifest path is about (`logos.dark`, `titleArt.file`, `ornament.dots[2]`).
function controlOfPath(path: string | undefined): Control | null {
  const [head, second] = (path ?? '').split(/[.[]/);
  if (head === 'logos') {
    return second === 'light' || second === 'dark' || second === 'showOnDark' ? second : null;
  }
  if (head === 'titleArt') return 'art';
  return head === 'ornament' ? 'dots' : null;
}

const WAVES = 'waves';

function choiceOf(art: TemplateManifest['titleArt']): ArtChoice | null {
  if (art.kind === 'image') return 'image';
  if (art.kind === 'none') return 'none';
  return art.generator === WAVES ? 'waves' : null;
}

const uploadMessage = (outcome: { kind: string; message: string }, fileName: string) =>
  outcome.kind === 'unreachable' ? `Could not upload ${fileName}. Try again.` : outcome.message;

// The Logo and art tab: the logo for light and for dark slides, the title art and the ornament
// dots. An upload stores the file, then saves the draft with its asset id and reads the draft again;
// each change saves at once, and focus goes back to the control that was used. A refused upload
// shows the server's sentence in an alert under its control; a refused save shows beside the
// control it is about; a failure with no field (no answer, a stale revision) is the frame's.
export function LogoArtTab({ draft, change, reload }: EditorTabProps) {
  const id = useId();
  const manifest = draft.manifest;
  const [busy, setBusy] = useState<Control | null>(null);
  const [uploadErrors, setUploadErrors] = useState<Partial<Record<Control, string>>>({});
  const [saveErrors, setSaveErrors] = useState<Partial<Record<Control, string>>>({});
  // What is on screen ahead of the server, until its save answers.
  const [pendingChoice, setPendingChoice] = useState<ArtChoice | null>(null);
  const [pendingOnDark, setPendingOnDark] = useState<boolean | null>(null);
  const [pendingDots, setPendingDots] = useState<string[] | null>(null);
  const dotsRef = useRef<string[] | null>(null);

  const assetOf = (assetId: string | undefined) =>
    assetId === undefined ? undefined : draft.assets.find((asset) => asset.assetId === assetId);
  const shown = pendingChoice ?? choiceOf(manifest.titleArt);
  const showOnDark = pendingOnDark ?? manifest.logos.showOnDark;
  const dots = pendingDots ?? manifest.ornament.dots;

  const focusControl = (control: Control) => {
    if (control === 'dots') return;
    const target = {
      light: `${id}-light`,
      dark: `${id}-dark`,
      art: `${id}-art-upload`,
      showOnDark: `${id}-showOnDark`,
    }[control];
    document.getElementById(target)?.focus();
  };

  const clear = (control: Control) => {
    setUploadErrors((current) => omit(current, control));
    setSaveErrors((current) => omit(current, control));
  };

  // A refusal shows beside its control and takes focus; other failures are the frame's.
  const settle = (control: Control, outcome: ChangeOutcome): boolean => {
    if (outcome.ok) return true;
    if (outcome.code === null || outcome.code === 'stale_revision') return false;
    const at = controlOfPath(outcome.path) ?? control;
    setSaveErrors((current) => ({ ...current, [at]: outcome.message }));
    focusControl(at);
    return false;
  };

  async function save(control: Control, patch: Partial<TemplateManifest>): Promise<boolean> {
    clear(control);
    return settle(control, await change(patch));
  }

  async function upload(
    control: 'light' | 'dark' | 'art',
    file: File,
    patchOf: (assetId: string) => Partial<TemplateManifest>,
  ): Promise<boolean> {
    setBusy(control);
    clear(control);
    try {
      const outcome = await uploadAsset(draft.id, control === 'art' ? 'art' : 'logo', file, false);
      if (!outcome.ok) {
        setUploadErrors((current) => ({
          ...current,
          [control]: uploadMessage(outcome, file.name),
        }));
        return false;
      }
      const saved = settle(control, await change(patchOf(outcome.asset.assetId)));
      await reload();
      return saved;
    } finally {
      setBusy(null);
      focusControl(control);
    }
  }

  const pickLogo = (side: LogoSide) => (file: File) =>
    void upload(side, file, (assetId) => ({ logos: { ...manifest.logos, [side]: assetId } }));

  async function removeLogo(side: LogoSide) {
    await save(side, { logos: omit(manifest.logos, side) });
    document.getElementById(`${id}-${side}`)?.focus();
  }

  async function setShowOnDark(value: boolean) {
    setPendingOnDark(value);
    await save('showOnDark', { logos: { ...manifest.logos, showOnDark: value } });
    setPendingOnDark(null);
  }

  async function chooseArt(choice: ArtChoice) {
    if (choice === choiceOf(manifest.titleArt)) {
      setPendingChoice(null);
      return;
    }
    setPendingChoice(choice);
    // An image is saved with its upload: until then the choice is only on screen.
    if (choice === 'image') {
      clear('art');
      return;
    }
    const titleArt =
      choice === 'waves'
        ? { kind: 'generator' as const, generator: WAVES }
        : { kind: 'none' as const };
    await save('art', { titleArt });
    setPendingChoice(null);
  }

  async function toggleDot(token: string) {
    const current = dotsRef.current ?? manifest.ornament.dots;
    const next = current.includes(token)
      ? current.filter((dot) => dot !== token)
      : [...current, token];
    dotsRef.current = next;
    setPendingDots(next);
    const saved = await save('dots', { ornament: { dots: next } });
    if (dotsRef.current === next) {
      dotsRef.current = null;
      setPendingDots(null);
    }
    if (!saved) document.getElementById(`${id}-dot-${token}`)?.focus();
  }

  return (
    <section
      data-testid={editorTabTestId('logo-art')}
      aria-label={`Logo and art of ${draft.name}`}
      className="panel grid gap-4 p-4"
    >
      <h2 id={`${id}-heading`} className="text-sm font-semibold text-ink">
        Logo and art
      </h2>

      <div className="grid gap-4 sm:grid-cols-2">
        {(['light', 'dark'] as const).map((side) => (
          <LogoSlot
            key={side}
            id={id}
            side={side}
            asset={assetOf(manifest.logos[side])}
            busy={busy !== null}
            uploadError={uploadErrors[side] ?? null}
            saveError={saveErrors[side] ?? null}
            onPick={pickLogo(side)}
            onRemove={() => void removeLogo(side)}
          />
        ))}
      </div>

      <div className="grid gap-1 text-meta">
        <label>
          <input
            id={`${id}-showOnDark`}
            type="checkbox"
            checked={showOnDark}
            aria-describedby={
              saveErrors.showOnDark === undefined ? undefined : `${id}-onDark-error`
            }
            onChange={(event) => void setShowOnDark(event.target.checked)}
          />{' '}
          Show the dark logo on title, divider and close slides
        </label>
        {saveErrors.showOnDark === undefined ? null : (
          <p id={`${id}-onDark-error`} className="text-bad">
            {saveErrors.showOnDark}
          </p>
        )}
      </div>

      <TitleArtChoice
        id={id}
        shown={shown}
        image={assetOf(manifest.titleArt.file)}
        busy={busy !== null}
        uploadError={uploadErrors.art ?? null}
        saveError={saveErrors.art ?? null}
        onChoose={(choice) => void chooseArt(choice)}
        onPick={(file) =>
          void upload('art', file, (assetId) => ({
            titleArt: { kind: 'image', file: assetId },
          })).then((saved) => saved && setPendingChoice(null))
        }
      />

      <OrnamentDots
        id={id}
        dots={dots}
        error={saveErrors.dots ?? null}
        onToggle={(t) => void toggleDot(t)}
      />
    </section>
  );
}
