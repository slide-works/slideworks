import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real divider layout replaces it.
export function DividerLayout({ slots }: LayoutProps<'divider'>) {
  return (
    <div data-testid="layout-divider" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['divider']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
