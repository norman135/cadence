import { cn } from '@/shared/lib/utils';

/** Initials in a circle; the hue is derived from the name so each person keeps the same color. */
export function Avatar({ name, className }: { name: string; className?: string }) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
  let hue = 0;
  for (let index = 0; index < name.length; index++) hue = (hue + name.charCodeAt(index)) % 360;

  return (
    <span
      aria-hidden="true"
      className={cn(
        'inline-flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold text-white',
        className,
      )}
      style={{ backgroundColor: `oklch(0.55 0.13 ${String(hue)})` }}
    >
      {initials || '?'}
    </span>
  );
}
