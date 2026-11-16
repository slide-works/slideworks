import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real compare layout replaces it.
export function CompareLayout({ slots }: LayoutProps<'compare'>) {
  return (
    <div data-testid="layout-compare" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['compare']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
