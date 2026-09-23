import type { ComponentProps } from 'react';

/** The Cadence mark: three rising bars, a steady rhythm of delivery. */
export function CadenceLogo(props: ComponentProps<'svg'>) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" {...props}>
      <rect width="32" height="32" rx="8" className="fill-primary" />
      <rect
        x="7"
        y="15"
        width="4"
        height="10"
        rx="2"
        className="fill-primary-foreground"
        opacity=".6"
      />
      <rect
        x="14"
        y="10"
        width="4"
        height="15"
        rx="2"
        className="fill-primary-foreground"
        opacity=".8"
      />
      <rect x="21" y="6" width="4" height="19" rx="2" className="fill-primary-foreground" />
    </svg>
  );
}
