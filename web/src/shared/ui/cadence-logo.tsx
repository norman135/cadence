import { useId, type ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

/**
 * The Cadence mark (design/logo/cadence-mark.svg): three beats rising on a Tempo tile, the last
 * one in Ember, the moment work ships.
 */
export function CadenceLogo(props: ComponentProps<'svg'>) {
  const gradient = useId();

  return (
    <svg viewBox="0 0 64 64" aria-hidden="true" {...props}>
      <defs>
        <linearGradient id={gradient} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#139f8f" />
          <stop offset="1" stopColor="#0b655d" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="16" fill={`url(#${gradient})`} />
      <rect x="15" y="33" width="8" height="16" rx="4" fill="#fff" fillOpacity="0.72" />
      <rect x="28" y="23" width="8" height="26" rx="4" fill="#fff" />
      <rect x="41" y="15" width="8" height="34" rx="4" fill="#ff7b45" />
    </svg>
  );
}

/** The mark and the lowercase wordmark, for headers and the sign-in page. */
export function CadenceWordmark({ className, ...props }: ComponentProps<'span'>) {
  return (
    <span className={cn('inline-flex items-center gap-2', className)} {...props}>
      <CadenceLogo className="size-[1.4em]" />
      <span className="font-semibold tracking-[-0.05em]">cadence</span>
    </span>
  );
}
