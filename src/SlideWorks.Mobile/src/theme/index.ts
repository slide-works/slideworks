import { useColorScheme } from 'react-native';
import { tokens } from './tokens.generated';

// The app's theme, built only from the generated design tokens: light follows the web's content slides, dark its
// night slides.
export type Colors = Record<
  | 'background'
  | 'surface'
  | 'text'
  | 'muted'
  | 'border'
  | 'chip'
  | 'accent'
  | 'onAccent'
  | 'good'
  | 'bad',
  string
>;

export const lightColors: Colors = {
  background: tokens.paper,
  surface: tokens.card,
  text: tokens.ink,
  muted: tokens.muted,
  border: tokens.grid,
  chip: tokens.chip,
  accent: tokens.accent,
  onAccent: tokens.onAccent,
  good: tokens.good,
  bad: tokens.bad,
};

export const darkColors: Colors = {
  background: tokens.night,
  surface: tokens.panelInverse,
  text: tokens.nightInk,
  muted: tokens.nightMuted,
  border: tokens.nightLine,
  chip: tokens.nightLine,
  accent: tokens.accentGlow,
  onAccent: tokens.night,
  good: tokens.goodSoft,
  bad: tokens.badSoft,
};

export const space = {
  xs: tokens.space1,
  sm: tokens.space2,
  md: tokens.space3,
  lg: tokens.space4,
  xl: tokens.space5,
} as const;

export const radius = {
  sm: tokens.radiusSm,
  md: tokens.radiusMd,
  lg: tokens.radiusLg,
  pill: tokens.radiusPill,
} as const;

export type ColorScheme = 'light' | 'dark';
export type Theme = {
  scheme: ColorScheme;
  colors: Colors;
  space: typeof space;
  radius: typeof radius;
};

export function themeFor(scheme: ColorScheme): Theme {
  return { scheme, colors: scheme === 'dark' ? darkColors : lightColors, space, radius };
}

export function useTheme(): Theme {
  return themeFor(useColorScheme() === 'dark' ? 'dark' : 'light');
}
