import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real big-number layout replaces it.
export function BigNumberLayout({ slots }: LayoutProps<'big-number'>) {
  return (
    <div data-testid="layout-big-number" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['big-number']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
