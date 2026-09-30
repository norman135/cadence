import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export function Input({ className, type = 'text', ...props }: ComponentProps<'input'>) {
  return (
    <input
      type={type}
      className={cn(
        'flex h-8 w-full min-w-0 rounded-md border border-input bg-card px-2.5 text-sm transition-[border-color,box-shadow] duration-[120ms] outline-none placeholder:text-subtle-foreground disabled:cursor-not-allowed disabled:opacity-50',
        'focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring',
        'aria-invalid:border-destructive aria-invalid:ring-[3px] aria-invalid:ring-destructive/20',
        className,
      )}
      {...props}
    />
  );
}
