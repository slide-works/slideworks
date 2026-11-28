import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { createDeviceSessionCode } from '@slideworks/core/deviceSessions';
import { ApiError } from '../api/client';
import type { MeView } from '../api/types';
import { DEVICE_SIGN_IN_TEST_IDS as IDS } from './deviceSignIn/testIds';

// What the app sends (contract K4's codes row): challenge is base64url(SHA-256(verifier)), state is 16–128 base64url
// characters, and the device name is 1–80 characters after trim.
const CHALLENGE = /^[A-Za-z0-9_-]{43}$/;
const STATE = /^[A-Za-z0-9_-]{16,128}$/;
const MAX_DEVICE_NAME = 80;

// Where the app listens for the end of the handoff (contract K6).
const APP_REDIRECT = 'slideworks://signed-in';

type Handoff = { challenge: string; state: string; deviceName: string };

type Step =
  | { kind: 'confirm'; failed: boolean }
  | { kind: 'minting' }
  | { kind: 'minted'; openAppUrl: string }
  | { kind: 'refused'; message: string };

function handoffOf(params: URLSearchParams): Handoff | null {
  const challenge = params.get('challenge') ?? '';
  const state = params.get('state') ?? '';
  const deviceName = (params.get('device') ?? '').trim();
  if (!CHALLENGE.test(challenge) || !STATE.test(state)) return null;
  if (deviceName.length === 0 || deviceName.length > MAX_DEVICE_NAME) return null;
  return { challenge, state, deviceName };
}

// /app/sign-in?challenge=&state=&device= (contract K6): the SlideWorks app opens it in the system browser to sign in.
// Continue mints a one-use code (K4) and then offers Open the app, a link carrying the code back to the app; Cancel
// tells the app the person said no. Pending people may use it (DeviceSignInGate), so the app can show them the
// pending answer itself.
export function DeviceSignInPage({ me }: { me: MeView }) {
  const [params] = useSearchParams();
  const handoff = handoffOf(params);
  const [step, setStep] = useState<Step>({ kind: 'confirm', failed: false });
  const openApp = useRef<HTMLAnchorElement>(null);
  const message = useRef<HTMLParagraphElement>(null);

  // After each answer, focus goes to what it brought: the Open the app link, or the message saying why not.
  useEffect(() => {
    if (step.kind === 'minted') openApp.current?.focus();
    if (step.kind === 'refused' || (step.kind === 'confirm' && step.failed)) {
      message.current?.focus();
    }
  }, [step]);

  if (handoff === null) {
    return (
      <main data-testid={IDS.page} className="mx-auto grid max-w-sm gap-4 px-4 py-16">
        <h1 className="page-title">Sign in to SlideWorks</h1>
        <p role="alert" data-testid={IDS.incomplete} className="text-meta text-ink">
          This sign-in link is incomplete. Start again from the SlideWorks app.
        </p>
      </main>
    );
  }

  const mint = async () => {
    setStep({ kind: 'minting' });
    try {
      const { openAppUrl } = await createDeviceSessionCode(handoff);
      setStep({ kind: 'minted', openAppUrl });
    } catch (error) {
      // A refusal carries the server's own sentence (K4); anything else is worth another try.
      setStep(
        error instanceof ApiError && error.detail !== undefined
          ? { kind: 'refused', message: error.detail }
          : { kind: 'confirm', failed: true },
      );
    }
  };

  const cancelUrl = `${APP_REDIRECT}?error=cancelled&state=${encodeURIComponent(handoff.state)}&iss=${encodeURIComponent(window.location.origin)}`;
  const minting = step.kind === 'minting';

  return (
    <main data-testid={IDS.page} className="mx-auto grid max-w-sm gap-4 px-4 py-16">
      <h1 data-testid={IDS.heading} className="page-title">
        Sign in to SlideWorks on {handoff.deviceName}?
      </h1>
      <p data-testid={IDS.email} className="text-meta text-ink">
        You're signing in as {me.email}.
      </p>
      <p
        data-testid={IDS.warning}
        className="rounded-panel border border-edge-strong bg-panel-2 px-3 py-2 text-meta text-ink"
      >
        Only continue if you just tapped Sign in in the SlideWorks app.
      </p>
      {step.kind === 'minted' ? (
        <a
          ref={openApp}
          href={step.openAppUrl}
          data-testid={IDS.openApp}
          className="btn-primary no-underline"
        >
          Open the app
        </a>
      ) : (
        <div className="flex gap-2">
          {step.kind !== 'refused' && (
            <button
              type="button"
              className="btn-primary"
              data-testid={IDS.continueButton}
              disabled={minting}
              aria-busy={minting}
              onClick={() => void mint()}
            >
              Continue
            </button>
          )}
          {minting ? (
            <a role="link" aria-disabled="true" data-testid={IDS.cancel} className="btn-ghost">
              Cancel
            </a>
          ) : (
            <a href={cancelUrl} data-testid={IDS.cancel} className="btn-ghost no-underline">
              Cancel
            </a>
          )}
        </div>
      )}
      {step.kind === 'refused' && (
        <p
          ref={message}
          tabIndex={-1}
          role="alert"
          data-testid={IDS.refused}
          className="text-meta text-ink"
        >
          {step.message}
        </p>
      )}
      {step.kind === 'confirm' && step.failed && (
        <p
          ref={message}
          tabIndex={-1}
          role="alert"
          data-testid={IDS.failed}
          className="text-meta text-ink"
        >
          Couldn't start the sign-in. Try again.
        </p>
      )}
    </main>
  );
}
