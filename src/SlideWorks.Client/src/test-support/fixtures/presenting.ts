// SW-4's contract samples (P1, P3, P4) as typed fixtures, built from the JSON in
// src/contracts. Each call returns a fresh copy, so a test may change its own.
// presenting.test.ts checks the exact key sets and the literal values at run time.
import presentationStateJson from '@contracts/presentation-state.json' with { type: 'json' };
import presentationsJson from '@contracts/screens/presentations.json' with { type: 'json' };
import presenterJson from '@contracts/screens/presenter.json' with { type: 'json' };
import type { LivePresentationsView, PresenterView } from '../../api/types';
import type { PresentationState } from '../../presentation/types';
import type { JsonView, Widen } from './widen';

const PRESENTER = presenterJson satisfies JsonView<PresenterView> as PresenterView;
const STATE = presentationStateJson satisfies Widen<PresentationState> as PresentationState;
const LIVE = presentationsJson satisfies Widen<LivePresentationsView> as LivePresentationsView;

// The sample deck "Ship smaller" as its owner presents it: 16 slides, notes on
// slides 1, 8, 10 and 12, deep dives 5, 10 and 12, and a 30-minute talk length.
export function presenterView(): PresenterView {
  return structuredClone(PRESENTER);
}

// The same deck live on slide 8, step 0, timer running at 12:40, with a
// presenter window, an audience window and a phone connected.
export function presentationState(overrides: Partial<PresentationState> = {}): PresentationState {
  return { ...structuredClone(STATE), ...overrides };
}

// One live presentation of the same deck, as the phone's pick page lists it.
export function livePresentations(): LivePresentationsView {
  return structuredClone(LIVE);
}
