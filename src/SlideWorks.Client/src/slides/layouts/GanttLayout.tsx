import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real gantt layout replaces it.
export function GanttLayout({ slots }: LayoutProps<'gantt'>) {
  return (
    <div data-testid="layout-gantt" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['gantt']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
