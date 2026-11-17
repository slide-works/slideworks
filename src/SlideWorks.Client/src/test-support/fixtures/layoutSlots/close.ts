import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { CloseSlots } from '../../../slides/slots/close';

// One valid close slide's slots, with every optional slot set.
export const SLOTS: CloseSlots = {
  headline: 'Pick one story.\nShip half of it by Friday.',
  sub: 'Then tell us what you learned at the next demo.',
  author: 'Sam Rivera · Product Engineering',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline'] as const satisfies readonly RequiredKey<CloseSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<CloseSlots, typeof REQUIRED>>;
