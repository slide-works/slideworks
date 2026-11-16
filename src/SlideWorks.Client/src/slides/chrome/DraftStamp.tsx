import { Stamp } from '../blocks/Stamp';

// The deck's draft mark on its title slide: the same stamp a title slot can
// carry, reading 'Draft'. The frame leaves it out when the slot has its own.
export function DraftStamp() {
  return <Stamp text="Draft" />;
}
