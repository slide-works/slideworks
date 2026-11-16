import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real bullets layout replaces it.
export function BulletsLayout({ slots }: LayoutProps<'bullets'>) {
  return (
    <div data-testid="layout-bullets" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['bullets']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
