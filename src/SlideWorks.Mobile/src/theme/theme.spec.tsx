import { renderHook } from '@testing-library/react-native';
import { darkColors, lightColors, useTheme } from '.';

jest.mock('react-native/Libraries/Utilities/useColorScheme', () => ({
  __esModule: true,
  default: jest.fn(),
}));
const useColorScheme: jest.Mock = jest.requireMock(
  'react-native/Libraries/Utilities/useColorScheme',
).default;

describe('theme', () => {
  it('follows the colour scheme', async () => {
    useColorScheme.mockReturnValue('dark');
    expect((await renderHook(() => useTheme())).result.current.colors).toBe(darkColors);

    useColorScheme.mockReturnValue('light');
    expect((await renderHook(() => useTheme())).result.current.colors).toBe(lightColors);
  });
});
