import { Shell } from './Shell';
import { SignedInGate } from './SignedInGate';

// The signed-in pages with the Shell's top bar around them, behind the auth gate (SignedInGate).
export function App() {
  return <SignedInGate>{(me) => <Shell me={me} />}</SignedInGate>;
}
