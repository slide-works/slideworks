import { useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { templateErrorCode, templateErrorMessage, templatesApi } from '../api';
import { ApiError } from '../../api/client';
import { TEMPLATES_TEST_IDS } from '../testIds';
import type { EditorTab, TemplateChecklistRow } from '../types';
import { TAB_PARAM, type EditorTabProps } from './tabProps';

const MARK: Record<TemplateChecklistRow['state'], { glyph: string; word: string; tone: string }> = {
  pass: { glyph: '✓', word: 'Passed', tone: 'text-ok' },
  fail: { glyph: '✗', word: 'Failed', tone: 'text-bad' },
  todo: { glyph: '!', word: 'To do', tone: 'text-warn' },
};

const PUBLISH_FAILED = 'Could not publish. Nothing was changed.';
const UPDATE_FAILED = 'Could not update your decks. Try again.';
const decks = (count: number) => `${count} ${count === 1 ? 'deck' : 'decks'}`;

type Outcome =
  | { kind: 'published'; version: number; behind: number }
  | { kind: 'updated'; count: number }
  | { kind: 'dismissed'; version: number };

// The publish checklist: the rows the server gives, a count of what is left, Publish (off until
// the server says canPublish) and, after a publish, the offer to move the person's own decks to
// the new version. The server's checklist decides; nothing here enables Publish by itself.
export function PublishChecklist({ draft, reload, saving }: EditorTabProps) {
  const location = useLocation();
  const [publishing, setPublishing] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [refusal, setRefusal] = useState<{ message: string; code: string | null } | null>(null);
  const [updating, setUpdating] = useState(false);
  const [updateError, setUpdateError] = useState<string | null>(null);
  const [focusRow, setFocusRow] = useState(0);
  const [focusMessage, setFocusMessage] = useState(0);
  const rows = useRef<HTMLUListElement>(null);
  const message = useRef<HTMLParagraphElement>(null);
  const publishButton = useRef<HTMLButtonElement>(null);

  const left = draft.checklist.filter((row) => row.state !== 'pass');
  const nextVersion = draft.version + 1;

  // After a refused publish the checklist is read again; focus then goes to the first row to fix.
  useEffect(() => {
    if (focusRow === 0) return;
    const first =
      rows.current?.querySelector<HTMLElement>('[data-state="fail"]') ??
      rows.current?.querySelector<HTMLElement>('[data-state="todo"]');
    first?.focus();
  }, [focusRow]);

  // The message after a publish or an update takes focus when it appears.
  useEffect(() => {
    if (focusMessage > 0) message.current?.focus();
  }, [focusMessage]);

  const tabLink = (tab: EditorTab) => {
    const search = new URLSearchParams(location.search);
    search.set(TAB_PARAM, tab);
    return { search: `?${search.toString()}` };
  };

  const publish = async () => {
    setPublishing(true);
    setRefusal(null);
    setOutcome(null);
    setUpdateError(null);
    try {
      const result = await templatesApi.publish(draft.id);
      setOutcome({ kind: 'published', version: result.version, behind: result.decksBehind });
      setFocusMessage((count) => count + 1);
      await reload();
    } catch (error) {
      const code = templateErrorCode(error);
      if (code === null || !(error instanceof ApiError)) {
        setRefusal({ message: PUBLISH_FAILED, code: null });
      } else {
        setRefusal({ message: templateErrorMessage(error), code });
        await reload();
        setFocusRow((count) => count + 1);
      }
    } finally {
      setPublishing(false);
    }
  };

  const updateDecks = async () => {
    setUpdating(true);
    setUpdateError(null);
    try {
      const result = await templatesApi.updateMyDecks(draft.id);
      setOutcome({ kind: 'updated', count: result.count });
      setFocusMessage((count) => count + 1);
    } catch (error) {
      setUpdateError(error instanceof ApiError && error.detail ? error.detail : UPDATE_FAILED);
    } finally {
      setUpdating(false);
    }
  };

  const reason = saving
    ? 'Saving your changes.'
    : left.length > 0
      ? `Fix ${left.length} ${left.length === 1 ? 'check' : 'checks'} to publish`
      : !draft.canPublish
        ? 'Not ready to publish yet.'
        : null;
  const blocked = publishing || saving || !draft.canPublish;

  return (
    <section
      data-testid={TEMPLATES_TEST_IDS.checklist}
      aria-label={`Publish checklist of ${draft.name}`}
      className="panel grid gap-3 p-4"
    >
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold text-ink">Publish checklist</h2>
        <span
          data-testid="checklist-count"
          className={`text-meta font-semibold ${left.length === 0 ? 'text-ok' : 'text-bad'}`}
        >
          {left.length === 0 ? 'All clear' : `${left.length} to fix`}
        </span>
      </div>

      <ul ref={rows} className="grid gap-2">
        {draft.checklist.map((row) => {
          const mark = MARK[row.state];
          return (
            <li
              key={row.key}
              tabIndex={-1}
              data-testid="checklist-row"
              data-state={row.state}
              data-key={row.key}
              className="flex items-start gap-2 text-meta"
            >
              <span className={`w-4 shrink-0 font-bold ${mark.tone}`} aria-hidden="true">
                {mark.glyph}
              </span>
              <span className="grid flex-1 gap-0.5">
                <span className="font-medium text-ink">
                  <span className="sr-only">{mark.word}: </span>
                  {row.label}
                </span>
                {row.detail !== '' && <span className="text-ink-muted">{row.detail}</span>}
              </span>
              {row.state !== 'pass' && (
                <Link
                  to={tabLink(row.tab)}
                  className="text-accent underline"
                  aria-label={`Fix ${row.label}`}
                >
                  Fix
                </Link>
              )}
            </li>
          );
        })}
      </ul>

      <div className="flex flex-wrap items-center gap-2">
        <button
          ref={publishButton}
          data-testid={TEMPLATES_TEST_IDS.publish}
          type="button"
          className="btn-primary"
          disabled={blocked}
          aria-describedby={reason === null ? undefined : 'publish-reason'}
          onClick={() => void publish()}
        >
          {publishing ? 'Publishing…' : `Publish version ${nextVersion}`}
        </button>
        {reason !== null && (
          <span id="publish-reason" className="text-meta text-ink-muted">
            {reason}
          </span>
        )}
      </div>

      {refusal !== null && (
        <p role="alert" className="text-meta text-bad">
          {refusal.message}
          {refusal.code === 'rights_not_confirmed' && (
            <>
              {' '}
              <Link to={tabLink('fonts')} className="underline">
                Go to the Fonts tab
              </Link>
            </>
          )}
        </p>
      )}

      {(outcome?.kind === 'published' || outcome?.kind === 'updated') && (
        <div className="grid gap-2 rounded-md bg-good-soft p-3 text-meta text-good-ink">
          <p ref={message} tabIndex={-1} data-testid="publish-message">
            {outcome.kind === 'updated' ? (
              `${decks(outcome.count)} updated.`
            ) : (
              <>
                <strong>Version {outcome.version} published.</strong>
                {outcome.behind > 0 &&
                  ` ${outcome.behind} of your decks ${outcome.behind === 1 ? 'is' : 'are'} on an older version.`}
              </>
            )}
          </p>
          {outcome.kind === 'published' && outcome.behind > 0 && (
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="btn-ghost"
                disabled={updating}
                onClick={() => void updateDecks()}
              >
                Update my {decks(outcome.behind)}
              </button>
              <button
                type="button"
                className="btn-ghost"
                onClick={() => {
                  setOutcome({ kind: 'dismissed', version: outcome.version });
                  publishButton.current?.focus();
                }}
              >
                Not now
              </button>
            </div>
          )}
          {updateError !== null && (
            <p role="alert" className="text-bad-ink">
              {updateError}
            </p>
          )}
        </div>
      )}
    </section>
  );
}
