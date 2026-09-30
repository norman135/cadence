import { cn } from '@/shared/lib/utils';

// Each carries white initials at 4.5:1 or more (WCAG AA); design/boards/board.js uses the same list.
const COLORS = [
  '#0b7a70',
  '#3b6fd6',
  '#7c4ddb',
  '#c93f7d',
  '#c0431a',
  '#8f6a0a',
  '#1b7a44',
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
