import { useEffect, useRef, useState } from 'react';

// The message for a settings change this part made. useQaControl's `error` is one value for the whole provider, so a
// refused moderation action in the panel would otherwise show under the settings controls too: a part keeps the message
// that was there when one of its own changes (`run`) settled, and shows it only while the provider still holds it.
export function useChangeError(error: string | null) {
  const [own, setOwn] = useState<string | null>(null);
  const latest = useRef(error);
  useEffect(() => {
    latest.current = error;
  }, [error]);
  const run = async (change: () => Promise<void>) => {
    setOwn(null);
    await change();
    setOwn(latest.current);
  };
  return { message: own !== null && own === error ? own : null, run };
}
