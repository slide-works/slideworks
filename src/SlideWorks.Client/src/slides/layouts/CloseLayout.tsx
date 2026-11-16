import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real close layout replaces it.
export function CloseLayout({ slots }: LayoutProps<'close'>) {
  return (
    <div data-testid="layout-close" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['close']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
