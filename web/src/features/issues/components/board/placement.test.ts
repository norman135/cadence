import { describe, expect, it } from 'vitest';
import { rankForDrop } from './placement';

const card = (id: string, rank: string) => ({ id, rank });
const a = card('a', 'a0');
const b = card('b', 'a1');
const c = card('c', 'a2');
const column = [a, b, c];

describe('rankForDrop', () => {
  it('fits between the neighbours at the drop position', () => {
    expect(rankForDrop(column, column, 1, null)).toBe('a0V');
    expect(rankForDrop(column, column, 0, null)).toBe('Zz');
    expect(rankForDrop(column, column, 3, null)).toBe('a3');
  });

  it('lands in an empty column', () => {
    expect(rankForDrop([], [], 0, null)).toBe('a0');
  });

  it('stays before the hidden rest of a partly shown column', () => {
    const rank = rankForDrop(column, column, 3, 'a3');
    expect(rank > 'a2' && rank < 'a3').toBe(true);
  });

  it('keeps a swimlane drop next to its lane neighbour in the whole column', () => {
    // The lane shows a and c; dropping after c must stay below b's neighbour order: after c.
    const lane = [a, c];
    const atEnd = rankForDrop(column, lane, 2, null);
    expect(atEnd > 'a2').toBe(true);

    // Dropping between a and c in the lane goes right before c, so after b in the column.
    const between = rankForDrop(column, lane, 1, null);
    expect(between > 'a1' && between < 'a2').toBe(true);
  });

  it('steps over equal ranks', () => {
    const tied = [card('a', 'a0'), card('b', 'a1'), card('c', 'a1'), card('d', 'a2')];
    const rank = rankForDrop(tied, tied, 2, null);
    expect(rank > 'a1' && rank < 'a2').toBe(true);
  });
});
