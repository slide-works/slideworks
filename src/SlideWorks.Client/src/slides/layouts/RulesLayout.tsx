import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real rules layout replaces it.
export function RulesLayout({ slots }: LayoutProps<'rules'>) {
  return (
    <div data-testid="layout-rules" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['rules']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
