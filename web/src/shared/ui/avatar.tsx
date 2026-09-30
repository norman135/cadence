import { cn } from '@/shared/lib/utils';

// Saturated enough for white initials in both themes (design/boards/board.js uses the same list).
const COLORS = [
  '#0b7a70',
  '#3b6fd6',
  '#7c4ddb',
  '#c93f7d',
  '#d44c1c',
  '#b7810b',
  '#1f8a4d',
  '#556274',
];

function colorFor(name: string) {
  let hash = 0;
  for (const character of name) hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
  return COLORS[hash % COLORS.length];
}

/** Initials in a circle; the color is derived from the name so each person keeps the same one. */
export function Avatar({ name, className }: { name: string; className?: string }) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');

  return (
    <span
      aria-hidden="true"
      className={cn(
        'inline-flex size-6 shrink-0 items-center justify-center rounded-full text-[10px] font-semibold text-white',
        className,
      )}
      style={{ backgroundColor: colorFor(name) }}
    >
      {initials || '?'}
    </span>
  );
}
