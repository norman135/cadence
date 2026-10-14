import { describe, expect, it } from 'vitest';
import { rankBetween } from './rank';

describe('rankBetween', () => {
  // The same vectors as the server's RankTests, so both implementations agree.
  it.each([
    [null, null, 'a0'],
    ['a0', null, 'a1'],
    ['a1', null, 'a2'],
    [null, 'a0', 'Zz'],
    ['a1', 'a2', 'a1V'],
    ['a0', 'a1', 'a0V'],
    ['az', null, 'b00'],
    [null, 'b00', 'az'],
    ['Zz', null, 'a0'],
    ['a0V', 'a1', 'a0l'],
    ['a0', 'a0V', 'a0G'],
    ['d0000', 'd0001', 'd0000V'],
    ['d0001', null, 'd0002'],
  ])('between %s and %s is %s', (before, after, expected) => {
    expect(rankBetween(before, after)).toBe(expected);
  });

  it('keeps keys ordered under repeated inserts at the same spot', () => {
    const low = rankBetween(null, null);
    let high = rankBetween(low, null);
    for (let i = 0; i < 200; i++) {
      const next = rankBetween(low, high);
      expect(next > low && next < high).toBe(true);
      high = next;
    }
  });

  it('refuses keys out of order', () => {
    expect(() => rankBetween('a1', 'a0')).toThrow();
  });
});
