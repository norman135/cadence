import { rankBetween } from '@/shared/lib/rank';

interface Ranked {
  id: string;
  rank: string;
}

/**
 * The rank for a card dropped into a column, where `lane` is what the person sees (the whole
 * column, or one swimlane of it) and `index` is the drop position in it. Neither list contains
 * the dragged card.
 *
 * The new rank sits between the visible neighbours, and stays inside the whole column's order: a
 * card dropped at the end of a swimlane goes right after its lane neighbour, not after every
 * card of the column. `nextRank` is the first hidden card's rank when the column is only partly
 * shown. Equal ranks (two concurrent creates) are skipped over, since no key fits between them.
 */
export function rankForDrop(
  column: readonly Ranked[],
  lane: readonly Ranked[],
  index: number,
  nextRank: string | null,
): string {
  const below = lane[index];
  let before: string | null;
  let after: string | null;

  if (below) {
    after = below.rank;
    const position = column.findIndex((card) => card.id === below.id);
    before = position > 0 ? (column[position - 1]?.rank ?? null) : null;
  } else {
    const above = lane[lane.length - 1];
    const position = above ? column.findIndex((card) => card.id === above.id) : column.length - 1;
    before = above ? above.rank : (column[column.length - 1]?.rank ?? null);
    after = column[position + 1]?.rank ?? nextRank;
  }

  if (before !== null && after !== null && after <= before) {
    // Ties: look further down the column for the first rank above `before`.
    after = column.map((card) => card.rank).find((rank) => rank > before) ?? null;
    if (after === null && nextRank !== null && nextRank > before) after = nextRank;
  }

  return rankBetween(before, after);
}
