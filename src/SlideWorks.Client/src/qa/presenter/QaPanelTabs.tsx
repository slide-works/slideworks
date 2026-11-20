import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { QA_PANEL_TEST_IDS as IDS } from '../testIds';

export type QaPanelTab = 'open' | 'review' | 'answered' | 'hidden';

export type QaPanelSort = 'top' | 'newest';

export type QaPanelTabCount = { tab: QaPanelTab; count: number };

type Props = {
  // The tabs to show, in order, with their counts. "To review" is left out by the panel when it isn't shown.
  tabs: QaPanelTabCount[];
  selected: QaPanelTab;
  onSelect: (tab: QaPanelTab) => void;
  sort: QaPanelSort;
  onSort: (sort: QaPanelSort) => void;
  // Each tab's id and the id of the tab panel the tabs control.
  tabId: (tab: QaPanelTab) => string;
  panelId: string;
};

const LABELS: Readonly<Record<QaPanelTab, string>> = {
  open: 'Open',
  review: 'To review',
  answered: 'Answered',
  hidden: 'Hidden',
};

const TEST_IDS: Readonly<Record<QaPanelTab, string>> = {
  open: IDS.tabOpen,
  review: IDS.tabReview,
  answered: IDS.tabAnswered,
  hidden: IDS.tabHidden,
};

const SORTS: readonly QaPanelSort[] = ['top', 'newest'];

const SORT_LABELS: Readonly<Record<QaPanelSort, string>> = { top: 'Top', newest: 'Newest' };

// The Questions panel's tab list (Open, To review, Answered, Hidden, each with its count) and the presenter's own sort.
// The tabs are a real tab list: arrow keys, Home and End move between them and select. "To review" carries a mark while
// something waits. The sort is a button ("Top ▾" / "Newest ▾") that opens a menu of the two; Escape closes it.
// Keys handled here are marked handled, so the presenter window doesn't also move the talk.
export function QaPanelTabs({ tabs, selected, onSelect, sort, onSort, tabId, panelId }: Props) {
  const tabRefs = useRef(new Map<QaPanelTab, HTMLButtonElement>());
  const sortButton = useRef<HTMLButtonElement>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuItems = useRef(new Map<QaPanelSort, HTMLButtonElement>());

  const onTabKey = (event: KeyboardEvent) => {
    const at = tabs.findIndex((t) => t.tab === selected);
    const next =
      event.key === 'ArrowRight'
        ? (at + 1) % tabs.length
        : event.key === 'ArrowLeft'
          ? (at - 1 + tabs.length) % tabs.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? tabs.length - 1
              : null;
    if (next === null) return;
    event.preventDefault();
    const tab = tabs[next].tab;
    onSelect(tab);
    tabRefs.current.get(tab)?.focus();
  };

  // An opened menu takes focus on its checked item.
  useEffect(() => {
    if (menuOpen) menuItems.current.get(sort)?.focus();
  }, [menuOpen, sort]);

  const closeMenu = (refocus: boolean) => {
    setMenuOpen(false);
    if (refocus) sortButton.current?.focus();
  };

  const onMenuKey = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeMenu(true);
      return;
    }
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
    event.preventDefault();
    const at = SORTS.findIndex((s) => menuItems.current.get(s) === document.activeElement);
    const next = (at + (event.key === 'ArrowDown' ? 1 : SORTS.length - 1)) % SORTS.length;
    menuItems.current.get(SORTS[next])?.focus();
  };

  return (
    <div className="flex flex-wrap items-center gap-1">
      <div
        data-testid={IDS.tabs}
        role="tablist"
        aria-label="Questions by state"
        className="flex flex-wrap items-center gap-1"
        onKeyDown={onTabKey}
      >
        {tabs.map(({ tab, count }) => {
          const on = tab === selected;
          const marked = tab === 'review' && count > 0;
          return (
            <button
              key={tab}
              ref={(element) => {
                if (element) tabRefs.current.set(tab, element);
                else tabRefs.current.delete(tab);
              }}
              id={tabId(tab)}
              type="button"
              role="tab"
              data-testid={TEST_IDS[tab]}
              aria-selected={on}
              aria-controls={panelId}
              tabIndex={on ? 0 : -1}
              onClick={() => onSelect(tab)}
              className={`inline-flex min-h-8 items-center gap-1.5 rounded-full px-2.5 py-1.5 text-[11.5px] leading-none font-semibold whitespace-nowrap focus-visible:outline-2 focus-visible:outline-[#8ea5ff] forced-colors:border forced-colors:border-[ButtonText] ${
                on ? 'bg-[#8ea5ff]/20 text-white' : 'bg-white/5 text-[#94a3b8]'
              } ${marked ? 'text-[#fde68a]' : ''}`}
            >
              {marked && (
                <span
                  data-testid={IDS.tabReviewMark}
                  aria-hidden="true"
                  className="size-1.5 rounded-full bg-[#fbbf24] forced-colors:bg-[CanvasText]"
                />
              )}
              {LABELS[tab]} {count}
            </button>
          );
        })}
      </div>
      <div
        className="relative ml-auto"
        onBlur={(event) => {
          if (menuOpen && !event.currentTarget.contains(event.relatedTarget)) closeMenu(false);
        }}
      >
        <button
          ref={sortButton}
          type="button"
          data-testid={IDS.sort}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          aria-label={`Sort: ${SORT_LABELS[sort]}`}
          onClick={() => (menuOpen ? closeMenu(false) : setMenuOpen(true))}
          className="min-h-8 rounded-full px-2.5 py-1.5 text-[11.5px] leading-none font-semibold text-[#94a3b8] focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
        >
          {SORT_LABELS[sort]} ▾
        </button>
        {menuOpen && (
          <div
            role="menu"
            aria-label="Sort questions"
            onKeyDown={onMenuKey}
            className="absolute right-0 z-10 mt-1 grid min-w-28 gap-0.5 rounded-lg border border-[#1f2937] bg-[#111827] p-1 shadow-lg"
          >
            {SORTS.map((option) => (
              <button
                key={option}
                ref={(element) => {
                  if (element) menuItems.current.set(option, element);
                  else menuItems.current.delete(option);
                }}
                type="button"
                role="menuitemradio"
                aria-checked={option === sort}
                data-testid={option === 'top' ? IDS.sortTop : IDS.sortNewest}
                tabIndex={-1}
                onClick={() => {
                  onSort(option);
                  closeMenu(true);
                }}
                className="min-h-8 rounded-md px-2.5 py-1.5 text-left text-[12px] text-[#e2e8f0] hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-[#8ea5ff] aria-checked:font-semibold"
              >
                {option === sort ? '✓ ' : ''}
                {SORT_LABELS[option]}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
