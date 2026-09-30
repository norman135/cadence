import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

/** A keyboard key, as in "Ctrl K". */
export function Kbd({ className, ...props }: ComponentProps<'kbd'>) {
  return (
    <kbd
      className={cn(
        'inline-grid h-5 min-w-5 place-items-center rounded-[5px] border border-b-2 bg-card px-1 font-mono text-[11px] font-normal text-muted-foreground',
        className,
      )}
      {...props}
    />
  );
}
