import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real practice layout replaces it.
export function PracticeLayout({ slots }: LayoutProps<'practice'>) {
  return (
    <div data-testid="layout-practice" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['practice']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
