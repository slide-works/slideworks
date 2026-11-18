import { useState } from 'react';

// The message for a settings change this part made. It is the value that call settled with (useQaControl's seam
// answers each call with its own message, or null), never the provider's shared `error` read after the await: another
// call can clear that meanwhile, or set it for a call that is not this part's. Starting a change clears the message of
// an earlier one; a change that fails shows its own message whenever it settles.
export function useChangeError() {
  const [message, setMessage] = useState<string | null>(null);
  const run = async (change: () => Promise<string | null>) => {
    setMessage(null);
    const outcome = await change();
    if (outcome !== null) setMessage(outcome);
  };
  return { message, run };
}
