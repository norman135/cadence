import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { setThemePreference, THEME_STORAGE_KEY, useTheme } from './theme';

describe('theme', () => {
  afterEach(() => {
    setThemePreference('system');
  });

  it('applies and remembers an explicit choice', () => {
    const { result } = renderHook(() => useTheme());

    act(() => {
      result.current.setPreference('dark');
    });

    expect(result.current.preference).toBe('dark');
    expect(result.current.resolved).toBe('dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('forgets the choice when going back to the system setting', () => {
    const { result } = renderHook(() => useTheme());

    act(() => {
      result.current.setPreference('dark');
    });
    act(() => {
      result.current.setPreference('system');
    });

    // jsdom has no matchMedia, so the system preference resolves to light.
    expect(result.current.resolved).toBe('light');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });
});
