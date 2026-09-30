import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

/** A styled native select: accessible and tiny, which suits short option lists like roles. */
export function Select({ className, ...props }: ComponentProps<'select'>) {
  return (
    <select
      className={cn(
        'h-8 rounded-md border border-input bg-card px-2.5 text-sm outline-none focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring disabled:opacity-50',
        className,
      )}
      {...props}
    />
  );
}
