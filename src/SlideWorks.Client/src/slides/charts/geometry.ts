// The chart's shared geometry, in canvas px, for ChartPlot and the regions it
// draws (ChartBars, ChartLines, ChartMarker).

// Series colours in order: the accent, the warning orange and the ink.
export const SERIES_COLORS = ['var(--sw-accent)', 'var(--sw-warn)', 'var(--sw-ink)'] as const;

// The plot's height; its width is the space the layout gives it.
export const PLOT_HEIGHT = 600;

// The top grid line's y, and the gap kept clear right of the plot.
export const TOP = 70;
export const RIGHT = 24;

export const FONT = 22;
export const VALUE_FONT = 24;
export const MARKER_FONT = 20;

// Average glyph widths (in em) used to decide where labels go. They err wide,
// so a label that is placed as fitting does fit. UNIT_EM covers the y unit at
// its widest: eight capital Ws measure 1.0 em each in Inter.
export const DIGIT_EM = 0.62;
export const LABEL_EM = 0.85;
export const UNIT_EM = 1.05;

export type Scale = (value: number) => number;
