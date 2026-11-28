import { useQuery } from '@tanstack/react-query';
import * as Application from 'expo-application';
import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { api } from '@slideworks/core/client';
import { endCurrentDeviceSession } from '@slideworks/core/deviceSessions';
import type { MeView } from '@slideworks/core/me';
import { links } from '../../links';
import { useAppRuntime, useSession } from '../../platform/appRuntime';
import { clearSignedInQueries, signedInKey } from '../../queries/queryClient';
import { useTheme } from '../../theme';
import { accountTestIds } from './testIds';

// Account (sheet): who is signed in, to which server, the app's version and build, and Sign out (asks first).
export function AccountScreen() {
  const { session, queryClient } = useAppRuntime();
  const snapshot = useSession();
  const { colors, space, radius } = useTheme();
  const [confirming, setConfirming] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const me = useQuery({ queryKey: signedInKey('me'), queryFn: () => api.get<MeView>('/api/me') });
  const server = snapshot.origin ?? '';

  // A later /api/me answer is what approves (or un-approves) a pending person.
  const access = me.data?.access;
  useEffect(() => {
    if (access) session.setPending(access === 'pending');
  }, [access, session]);

  async function signOut() {
    setSigningOut(true);
    // The server call may fail (offline, already ended); the phone signs out either way.
    await endCurrentDeviceSession().catch(() => undefined);
    session.clearSecret();
    clearSignedInQueries(queryClient);
    router.replace(links.home());
  }

  const text = { color: colors.text };
  const muted = { color: colors.muted };
  const button = { borderRadius: radius.md, padding: space.md };

  return (
    <View
      testID={accountTestIds.root}
      style={[
        styles.screen,
        { backgroundColor: colors.background, padding: space.lg, gap: space.sm },
      ]}
    >
      <Text testID={accountTestIds.name} style={[styles.name, text]}>
        {me.data?.name ?? ''}
      </Text>
      <Text testID={accountTestIds.email} style={text}>
        {me.data?.email ?? snapshot.email ?? ''}
      </Text>
      <Text testID={accountTestIds.server} style={muted}>
        {server}
      </Text>
      {snapshot.pending ? (
        <Text testID={accountTestIds.pending} style={[text, { marginTop: space.md }]}>
          Your account is waiting for approval.
        </Text>
      ) : null}
      <Text testID={accountTestIds.version} style={[muted, { marginTop: space.md }]}>
        Version {Application.nativeApplicationVersion} ({Application.nativeBuildVersion})
      </Text>
      {confirming ? (
        <View style={{ gap: space.sm, marginTop: space.lg }}>
          <Text style={text}>Sign out of {server}?</Text>
          <View style={[styles.row, { gap: space.md }]}>
            <Pressable
              testID={accountTestIds.cancelSignOut}
              accessibilityRole="button"
              disabled={signingOut}
              onPress={() => setConfirming(false)}
              style={[button, styles.grow, { backgroundColor: colors.chip }]}
            >
              <Text style={[styles.center, text]}>Cancel</Text>
            </Pressable>
            <Pressable
              testID={accountTestIds.confirmSignOut}
              accessibilityRole="button"
              disabled={signingOut}
              onPress={() => void signOut()}
              style={[button, styles.grow, { backgroundColor: colors.bad }]}
            >
              <Text style={[styles.center, styles.strong, { color: colors.onAccent }]}>
                Sign out
              </Text>
            </Pressable>
          </View>
        </View>
      ) : (
        <Pressable
          testID={accountTestIds.signOut}
          accessibilityRole="button"
          onPress={() => setConfirming(true)}
          style={[button, { marginTop: space.lg, borderWidth: 1, borderColor: colors.border }]}
        >
          <Text style={[styles.center, styles.strong, { color: colors.bad }]}>Sign out</Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  name: { fontSize: 22, fontWeight: '700' },
  row: { flexDirection: 'row' },
  grow: { flex: 1 },
  center: { textAlign: 'center' },
  strong: { fontWeight: '600' },
});
