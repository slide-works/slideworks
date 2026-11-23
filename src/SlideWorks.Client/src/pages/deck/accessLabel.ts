import type { DeckAccessLevel } from '../../api/types';

// D25: the wire says read and write; people read "Can view" and "Can edit".
export function accessLabel(access: DeckAccessLevel): string {
  return access === 'write' ? 'Can edit' : 'Can view';
}
