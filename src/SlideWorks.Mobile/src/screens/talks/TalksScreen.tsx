import type { DeckRowSummary } from '@slideworks/core/decks';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Pressable, SectionList, StyleSheet, Text, View } from 'react-native';
import { links } from '../../links';
import { useTheme, type Theme } from '../../theme';
import { talksTestIds } from './testIds';
import { talksQuery } from './talksQuery';

// The signed-in person's decks and the decks shared with them (D14: the title and when it was last updated). Read only.
export function TalksScreen() {
  const theme = useTheme();
  const styles = stylesFor(theme);
  const talks = useQuery(talksQuery);
  const [pulling, setPulling] = useState(false);

  const refresh = async () => {
    setPulling(true);
    try {
      await talks.refetch();
    } finally {
      setPulling(false);
    }
  };

  if (talks.data === undefined) {
    return (
      <View testID={talksTestIds.root} style={[styles.screen, styles.centred]}>
        {talks.isError ? (
          <View testID={talksTestIds.failed} style={styles.centred}>
            <Text style={styles.message}>Couldn&apos;t load your talks.</Text>
            <Pressable
              testID={talksTestIds.retry}
              accessibilityRole="button"
              onPress={() => void talks.refetch()}
              style={styles.button}
            >
              <Text style={styles.buttonText}>Try again</Text>
            </Pressable>
          </View>
        ) : (
          <ActivityIndicator testID={talksTestIds.loading} color={theme.colors.accent} />
        )}
      </View>
    );
  }

  const { decks, sharedWithMe } = talks.data;
  const sections = [
    { key: 'mine', title: 'Your talks', data: decks },
    { key: 'shared', title: 'Shared with you', data: sharedWithMe },
  ].filter((section) => section.data.length > 0);

  return (
    <View testID={talksTestIds.root} style={styles.screen}>
      <SectionList
        testID={talksTestIds.list}
        sections={sections}
        keyExtractor={(deck) => deck.id}
        refreshing={pulling}
        onRefresh={() => void refresh()}
        contentContainerStyle={sections.length === 0 ? styles.centred : undefined}
        ListEmptyComponent={
          <Text testID={talksTestIds.empty} style={styles.message}>
            No talks yet. Make one on the web.
          </Text>
        }
        renderSectionHeader={({ section }) => <Text style={styles.heading}>{section.title}</Text>}
        renderItem={({ item }) => <TalkRow deck={item} styles={styles} />}
      />
    </View>
  );
}

function TalkRow({ deck, styles }: { deck: DeckRowSummary; styles: Styles }) {
  return (
    <Pressable
      testID={talksTestIds.row(deck.id)}
      accessibilityRole="button"
      onPress={() => router.push(links.talk(deck.id))}
      style={styles.row}
    >
      <Text style={styles.title}>{deck.title}</Text>
      <Text style={styles.detail}>Updated {deck.updatedLabel}</Text>
    </Pressable>
  );
}

type Styles = ReturnType<typeof stylesFor>;

function stylesFor({ colors, space, radius }: Theme) {
  return StyleSheet.create({
    screen: { flex: 1, backgroundColor: colors.background },
    centred: { flexGrow: 1, alignItems: 'center', justifyContent: 'center', gap: space.md },
    message: { color: colors.muted, fontSize: 16, textAlign: 'center', padding: space.lg },
    heading: {
      color: colors.muted,
      fontSize: 13,
      fontWeight: '600',
      paddingHorizontal: space.lg,
      paddingTop: space.lg,
      paddingBottom: space.sm,
      backgroundColor: colors.background,
    },
    row: {
      marginHorizontal: space.lg,
      marginBottom: space.sm,
      padding: space.md,
      borderRadius: radius.md,
      backgroundColor: colors.surface,
      borderWidth: StyleSheet.hairlineWidth,
      borderColor: colors.border,
    },
    title: { color: colors.text, fontSize: 17, fontWeight: '600' },
    detail: { color: colors.muted, fontSize: 14, marginTop: space.xs },
    button: {
      backgroundColor: colors.accent,
      borderRadius: radius.pill,
      paddingHorizontal: space.lg,
      paddingVertical: space.sm,
    },
    buttonText: { color: colors.onAccent, fontSize: 16, fontWeight: '600' },
  });
}
