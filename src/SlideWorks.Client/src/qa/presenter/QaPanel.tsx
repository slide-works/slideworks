import { useEffect, useId, useRef, useState } from 'react';
import { QA_PANEL_TEST_IDS as IDS, qaPanelRowTestId } from '../testIds';
import type { QaAction, QaControlView, QaQuestion } from '../types';
import { QaPanelRow } from './QaPanelRow';
import { QaPanelTabs, type QaPanelSort, type QaPanelTab } from './QaPanelTabs';
import { QaSettingsStrip } from './QaSettingsStrip';
import { ReactionCounter } from './ReactionCounter';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

// The tab a question is in: hidden wins over the state under it.
function tabOf(question: QaQuestion): QaPanelTab {
  if (question.hidden) return 'hidden';
  return question.state === 'waiting' ? 'review' : question.state;
}

// The view's counts per tab.
function countOf(control: QaControlView, tab: QaPanelTab): number {
  const { counts } = control;
  return tab === 'review' ? counts.waiting : counts[tab];
}

// Top: most votes first, ties to the older question (lower number). Newest: the latest asked first.
function sorted(questions: QaQuestion[], sort: QaPanelSort): QaQuestion[] {
  return [...questions].sort((a, b) =>
    sort === 'top' ? b.votes - a.votes || a.number - b.number : b.number - a.number,
  );
}

const EMPTY: Readonly<Record<Exclude<QaPanelTab, 'open'>, { id: string; text: string }>> = {
  review: { id: IDS.emptyReview, text: 'Nothing waiting for approval.' },
  answered: { id: IDS.emptyAnswered, text: 'Nothing answered yet.' },
  hidden: { id: IDS.emptyHidden, text: 'Nothing hidden.' },
};

// After an action: where focus goes if the button pressed is gone (the question moved tabs, or its buttons changed).
type PendingFocus = { number: number; index: number };

// The Questions panel in the presenter window (SW-9 Q9), under QaControlLive, opened and closed with Q or the tool
// bar's Questions tool: the tabs Open, To review (only while review is on or something waits), Answered and Hidden; the
// presenter's own sort; each question's moderation buttons; the settings strip and the reactions counter. A refused or
// failed call's message shows at the top. After an action focus stays in the panel: on the same row if it is still in
// the tab, else on the row that took its place.
export function QaPanel({ deckId }: Props) {
  const qa = useQaControl(deckId);
  const { control, error } = qa;
  const [chosen, setChosen] = useState<QaPanelTab>('open');
  const [sort, setSort] = useState<QaPanelSort>('top');
  const [busy, setBusy] = useState(false);
  // The same, read synchronously, so two presses before a render still send one call.
  const sending = useRef(false);
  const pending = useRef<PendingFocus | null>(null);
  const root = useRef<HTMLElement>(null);
  const rowsRef = useRef<HTMLUListElement>(null);
  const ids = useId();
  const tabId = (tab: QaPanelTab) => `${ids}-tab-${tab}`;
  const panelId = `${ids}-tabpanel`;

  const showReview = control !== null && (control.settings.review || control.counts.waiting > 0);
  const tab = chosen === 'review' && !showReview ? 'open' : chosen;
  const shown =
    control === null
      ? []
      : sorted(
          control.questions.filter((q) => tabOf(q) === tab),
          sort,
        );

  const run = async (action: QaAction, number: number) => {
    if (sending.current) return;
    sending.current = true;
    const hadFocus = root.current?.contains(document.activeElement) ?? false;
    const index = shown.findIndex((q) => q.number === number);
    setBusy(true);
    await qa[action](number);
    sending.current = false;
    pending.current = hadFocus ? { number, index } : null;
    setBusy(false);
  };

  // Once the answer is drawn: if the pressed button is gone, focus the same row's first button, else the row now in
  // its place (or the last), else the panel.
  useEffect(() => {
    const target = pending.current;
    if (busy || target === null) return;
    pending.current = null;
    const panel = root.current;
    if (panel === null || panel.contains(document.activeElement)) return;
    const rows = [...(rowsRef.current?.children ?? [])] as HTMLElement[];
    const same = rows.find((row) => row.dataset.testid === qaPanelRowTestId(target.number));
    const row = same ?? rows[Math.min(target.index, rows.length - 1)];
    (row?.querySelector('button') ?? panel).focus();
  }, [busy, control]);

  const live = control !== null && control.live;
  const off = live && control.settings.questions === 'off';

  return (
    <section
      ref={root}
      data-testid={IDS.panel}
      aria-label="Questions"
      tabIndex={-1}
      className="grid min-w-0 content-start gap-2 outline-none"
    >
      <h2 className="text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">
        Questions
      </h2>
      <div className="grid gap-2.5 rounded-lg border border-[#1f2937] bg-[#111827] px-3.5 py-3">
        {error !== null && (
          <p
            data-testid={IDS.error}
            role="alert"
            className="rounded-md bg-[#c0362c]/25 px-3 py-2 text-[12.5px] text-[#fca5a5]"
          >
            {error}
          </p>
        )}
        {control !== null && !live && (
          <p data-testid={IDS.notLive} className="py-1.5 text-[12.5px] text-[#94a3b8]">
            Questions start when you go live.
          </p>
        )}
        {off && (
          <p className="py-1.5 text-[12.5px] text-[#94a3b8]">
            Questions are off for this deck. Nothing can be asked or upvoted.
          </p>
        )}
        {live && !off && (
          <>
            <QaPanelTabs
              tabs={(['open', 'review', 'answered', 'hidden'] as const)
                .filter((t) => t !== 'review' || showReview)
                .map((t) => ({ tab: t, count: countOf(control, t) }))}
              selected={tab}
              onSelect={setChosen}
              sort={sort}
              onSort={setSort}
              tabId={tabId}
              panelId={panelId}
            />
            <div
              id={panelId}
              role="tabpanel"
              aria-labelledby={tabId(tab)}
              className="max-h-80 overflow-y-auto"
            >
              {shown.length > 0 ? (
                <ul ref={rowsRef} data-testid={IDS.rows} className="grid content-start gap-1.5">
                  {shown.map((question) => (
                    <QaPanelRow
                      key={question.number}
                      question={question}
                      showOn={control.settings.showOn}
                      onAction={(action, number) => void run(action, number)}
                    />
                  ))}
                </ul>
              ) : tab === 'open' && control.settings.questions === 'closed' ? (
                <p data-testid={IDS.closed} className="px-0.5 py-1.5 text-[12.5px] text-[#94a3b8]">
                  Questions are closed. Nothing new can be asked.
                </p>
              ) : tab === 'open' ? (
                <p
                  data-testid={IDS.emptyOpen}
                  className="px-0.5 py-1.5 text-[12.5px] text-[#94a3b8]"
                >
                  No open questions. They appear here as the room asks.
                </p>
              ) : (
                <p
                  data-testid={EMPTY[tab].id}
                  className="px-0.5 py-1.5 text-[12.5px] text-[#94a3b8]"
                >
                  {EMPTY[tab].text}
                </p>
              )}
            </div>
          </>
        )}
        <div className="flex flex-wrap items-center gap-x-3.5 gap-y-1.5 border-t border-[#1f2937] pt-2.5 text-[11.5px] text-[#94a3b8]">
          <QaSettingsStrip deckId={deckId} />
          {control !== null && (
            <ReactionCounter enabled={control.settings.reactions} counts={control.reactions} />
          )}
        </div>
      </div>
    </section>
  );
}
