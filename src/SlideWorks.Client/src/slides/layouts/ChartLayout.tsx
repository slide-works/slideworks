import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real chart layout replaces it.
export function ChartLayout({ slots }: LayoutProps<'chart'>) {
  return (
    <div data-testid="layout-chart" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['chart']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
