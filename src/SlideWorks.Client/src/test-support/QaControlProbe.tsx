import { useQaControl } from '../qa/presenter/useQaControl';
import { QA_ACTIONS } from '../qa/types';

// Component tests only: prints what useQaControl gives for deckId, as JSON, and offers a button per action, so a spec
// can assert a provider's values and calls while the presenter's parts are still stubs. Each action button acts on
// question `number`; the settings button turns review off and the slide button turns slide 3's questions back on.
export function QaControlProbe({ deckId, number = 2 }: { deckId: string; number?: number }) {
  const value = useQaControl(deckId);
  return (
    <div>
      <pre data-testid="probe-qa-control">{JSON.stringify(value.control)}</pre>
      <pre data-testid="probe-qa-error">{JSON.stringify(value.error)}</pre>
      {QA_ACTIONS.map((action) => (
        <button
          key={action}
          type="button"
          data-testid={`probe-qa-${action}`}
          onClick={() => void value[action](number)}
        >
          {action}
        </button>
      ))}
      <button
        type="button"
        data-testid="probe-qa-settings"
        onClick={() => void value.setSettings({ review: false })}
      >
        settings
      </button>
      <button
        type="button"
        data-testid="probe-qa-slide"
        onClick={() => void value.setSlide(3, { questions: true })}
      >
        slide
      </button>
    </div>
  );
}
