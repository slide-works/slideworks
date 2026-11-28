import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { AccessibilityInfo, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { links } from '../../links';
import { useSession } from '../../platform/appRuntime';
import { useTheme, type Theme } from '../../theme';
import { isJoinCode } from './joinCode';
import { homeTestIds } from './testIds';

const INVALID_MESSAGE = 'Enter the six-digit code.';

// Home: Join a talk by its six-digit code (on the hosted server; join links name their own), and Sign in, or Talks
// once signed in. Joining never needs a session.
export function HomeScreen() {
  const theme = useTheme();
  const styles = stylesFor(theme);
  const { signedIn } = useSession();
  const [code, setCode] = useState('');
  const [invalid, setInvalid] = useState(false);

  const input = useRef<TextInput>(null);

  const join = () => {
    const entered = code.trim();
    if (!isJoinCode(entered)) {
      setInvalid(true);
      // Keyboard submit blurs the field: put the person back in it, and have a screen reader say what is wrong.
      input.current?.focus();
      AccessibilityInfo.announceForAccessibility(INVALID_MESSAGE);
      return;
    }
    setInvalid(false);
    router.push(links.join(entered));
  };

  return (
    <View testID={homeTestIds.root} style={styles.screen}>
      <Text style={styles.heading}>Join a talk</Text>
      <TextInput
        ref={input}
        testID={homeTestIds.codeInput}
        style={styles.input}
        value={code}
        onChangeText={(text) => {
          setCode(text);
          setInvalid(false);
        }}
        onSubmitEditing={join}
        keyboardType="number-pad"
        inputMode="numeric"
        textContentType="oneTimeCode"
        autoComplete="one-time-code"
        maxLength={6}
        placeholder="123456"
        placeholderTextColor={theme.colors.muted}
        accessibilityLabel="Join code"
      />
      <Text
        testID={invalid ? homeTestIds.codeInvalid : undefined}
        accessibilityRole={invalid ? 'alert' : undefined}
        accessibilityLiveRegion={invalid ? 'polite' : 'none'}
        style={[styles.hint, invalid && styles.hintInvalid]}
      >
        {INVALID_MESSAGE}
      </Text>
      <Pressable
        testID={homeTestIds.join}
        accessibilityRole="button"
        style={styles.primary}
        onPress={join}
      >
        <Text style={styles.primaryText}>Join a talk</Text>
      </Pressable>
      {signedIn ? (
        <Pressable
          testID={homeTestIds.talks}
          accessibilityRole="button"
          style={styles.secondary}
          onPress={() => router.push(links.talks())}
        >
          <Text style={styles.secondaryText}>Talks</Text>
        </Pressable>
      ) : (
        <Pressable
          testID={homeTestIds.signIn}
          accessibilityRole="button"
          style={styles.secondary}
          onPress={() => router.push(links.signIn())}
        >
          <Text style={styles.secondaryText}>Sign in</Text>
        </Pressable>
      )}
    </View>
  );
}

function stylesFor({ colors, space, radius }: Theme) {
  return StyleSheet.create({
    screen: { flex: 1, padding: space.lg, gap: space.md, backgroundColor: colors.background },
    heading: { fontSize: 24, fontWeight: '700', color: colors.text },
    input: {
      borderWidth: 1,
      borderColor: colors.border,
      borderRadius: radius.md,
      padding: space.md,
      fontSize: 28,
      letterSpacing: 6,
      color: colors.text,
      backgroundColor: colors.surface,
    },
    hint: { color: colors.muted },
    hintInvalid: { color: colors.bad },
    primary: {
      borderRadius: radius.md,
      padding: space.md,
      alignItems: 'center',
      backgroundColor: colors.accent,
    },
    primaryText: { fontWeight: '700', color: colors.onAccent },
    secondary: {
      borderRadius: radius.md,
      padding: space.md,
      alignItems: 'center',
      borderWidth: 1,
      borderColor: colors.border,
    },
    secondaryText: { fontWeight: '600', color: colors.text },
  });
}
