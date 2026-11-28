import { router } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { ApiError } from '@slideworks/core/client';
import { exchangeDeviceSessionCode } from '@slideworks/core/deviceSessions';
import type { MeView } from '@slideworks/core/me';
import { HOSTED_SERVER, links } from '../../links';
import { useAppRuntime } from '../../platform/appRuntime';
import { signedInKey } from '../../queries/queryClient';
import {
  challengeFor,
  httpRefused,
  newState,
  newVerifier,
  normaliseOrigin,
  parseRedirect,
  SIGN_IN_REDIRECT,
  signInUrl,
} from '../../signIn/handoff';
import { useTheme } from '../../theme';
import { deviceName, randomBytes, sha256Base64 } from './handoffDevice';
import { serverReachable } from './serverCheck';
import { signInTestIds } from './testIds';

// Typing settles before the server check runs.
const CHECK_DELAY_MS = 300;

const GENERIC_ERROR = "Couldn't sign in. Try again.";

type Check = { origin: string; reachable: boolean | null };

// Sign in (contract K6): the server prefilled (D3), a live server check, and the handoff through the system browser to
// a device session. The server words every refusal; a cancelled handoff returns here quietly.
export function SignInScreen() {
  const { session, queryClient } = useAppRuntime();
  const { colors, space, radius } = useTheme();
  const [typed, setTyped] = useState(() => session.lastServer() ?? HOSTED_SERVER);
  const [editing, setEditing] = useState(false);
  const [check, setCheck] = useState<Check | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const origin = normaliseOrigin(typed);

  useEffect(() => {
    if (!origin) return;
    let live = true;
    const timer = setTimeout(() => {
      void serverReachable(origin).then((reachable) => {
        if (live) setCheck({ origin, reachable });
      });
    }, CHECK_DELAY_MS);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [origin]);

  const reachable = origin !== null && check?.origin === origin ? check.reachable : null;
  const canSignIn = origin !== null && reachable === true && !busy;

  async function signIn(server: string) {
    setBusy(true);
    setError(null);
    // True from beginSignIn until commit: a failure in between must leave the session signed out.
    let begun = false;
    try {
      const verifier = newVerifier(randomBytes);
      const state = newState(randomBytes);
      const challenge = await challengeFor(verifier, sha256Base64);
      const url = signInUrl(server, { challenge, state, deviceName: deviceName() });
      const result = await WebBrowser.openAuthSessionAsync(url, SIGN_IN_REDIRECT);
      // Only a person closing the browser is a quiet return; anything else (such as 'locked') is a failure.
      if (result.type === 'cancel' || result.type === 'dismiss') return;
      if (result.type !== 'success') {
        setError(GENERIC_ERROR);
        return;
      }

      const redirect = parseRedirect(result.url, { state, origin: server });
      if (redirect.kind === 'cancelled') return;
      if (redirect.kind === 'foreign') {
        setError(`Sign-in didn't come from ${server}. Try again.`);
        return;
      }
      if (redirect.kind === 'incomplete') {
        setError(GENERIC_ERROR);
        return;
      }

      // From here every request goes to the typed server, which apiOrigin() now names.
      session.beginSignIn(server);
      begun = true;
      let me: MeView;
      try {
        const exchanged = await exchangeDeviceSessionCode({ code: redirect.code, verifier });
        session.setPendingSecret(exchanged.secret);
        me = exchanged.me;
        session.commit(me.email, me.access === 'pending');
        begun = false;
      } catch (failure) {
        session.abandon();
        begun = false;
        setError(failure instanceof ApiError && failure.detail ? failure.detail : GENERIC_ERROR);
        return;
      }
      queryClient.setQueryData(signedInKey('me'), me);
      // A pending person waits on Account, which says so, until the server approves them.
      router.replace(me.access === 'pending' ? links.account() : links.talks());
    } catch {
      // The crypto helpers or the browser session threw.
      if (begun) session.abandon();
      setError(GENERIC_ERROR);
    } finally {
      setBusy(false);
    }
  }

  return (
    <View
      testID={signInTestIds.root}
      style={[
        styles.screen,
        { backgroundColor: colors.background, padding: space.lg, gap: space.md },
      ]}
    >
      <Text style={[styles.label, { color: colors.muted }]}>Server</Text>
      {editing ? (
        <TextInput
          testID={signInTestIds.serverInput}
          accessibilityLabel="Server address"
          value={typed}
          onChangeText={setTyped}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="url"
          inputMode="url"
          placeholder={HOSTED_SERVER}
          placeholderTextColor={colors.muted}
          style={[
            styles.input,
            {
              color: colors.text,
              borderColor: colors.border,
              backgroundColor: colors.surface,
              borderRadius: radius.md,
              padding: space.sm,
            },
          ]}
        />
      ) : (
        <View style={[styles.row, { gap: space.md }]}>
          <Text testID={signInTestIds.server} style={[styles.server, { color: colors.text }]}>
            {origin ?? typed}
          </Text>
          <Pressable
            testID={signInTestIds.useAnotherServer}
            accessibilityRole="button"
            onPress={() => setEditing(true)}
          >
            <Text style={{ color: colors.accent }}>Use another server</Text>
          </Pressable>
        </View>
      )}
      {origin !== null && reachable === false ? (
        <Text testID={signInTestIds.unreachable} style={{ color: colors.bad }}>
          Can&apos;t reach {origin}.
        </Text>
      ) : null}
      {httpRefused(typed) ? (
        <Text testID={signInTestIds.httpRefused} style={{ color: colors.bad }}>
          Use an https address. Plain http works only for servers on your own computer or network.
        </Text>
      ) : null}
      <Pressable
        testID={signInTestIds.signIn}
        accessibilityRole="button"
        accessibilityState={{ disabled: !canSignIn, busy }}
        disabled={!canSignIn}
        onPress={() => {
          if (origin) void signIn(origin);
        }}
        style={[
          styles.button,
          {
            backgroundColor: colors.accent,
            borderRadius: radius.md,
            padding: space.md,
            opacity: canSignIn ? 1 : 0.5,
          },
        ]}
      >
        <Text style={[styles.buttonText, { color: colors.onAccent }]}>Sign in with Google</Text>
      </Pressable>
      {error ? (
        <Text testID={signInTestIds.error} style={{ color: colors.bad }}>
          {error}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  label: { fontSize: 13 },
  row: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap' },
  server: { fontSize: 17, fontWeight: '600' },
  input: { borderWidth: StyleSheet.hairlineWidth, fontSize: 17 },
  button: { alignItems: 'center' },
  buttonText: { fontSize: 17, fontWeight: '600' },
});
