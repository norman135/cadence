import { describe, expect, it } from 'vitest';
import { suggestKey } from './suggest-key';

describe('suggestKey', () => {
  it.each([
    ['Atlas Mobile', 'AM'],
    ['Nimbus', 'NIM'],
    ['web', 'WEB'],
    ['Customer-facing web app', 'CWA'],
    ['2026 Planning', 'PLA'],
    ['X', 'XX'],
    ['', 'PRJ'],
  ])('suggests a key for "%s" like the server does', (name, key) => {
    expect(suggestKey(name)).toBe(key);
  });
});
