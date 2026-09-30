import { Switch as Primitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export function Switch({ className, ...props }: ComponentProps<typeof Primitive.Root>) {
  return (
    <Primitive.Root
      className={cn(
        'relative inline-flex h-[18px] w-8 shrink-0 cursor-default rounded-full bg-input transition-colors duration-[120ms] outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:opacity-50 data-[state=checked]:bg-primary',
        className,
      )}
      {...props}
    >
      <Primitive.Thumb className="block size-3.5 translate-x-0.5 translate-y-0.5 rounded-full bg-white shadow-[0_1px_2px_rgb(0_0_0/0.25)] transition-transform duration-[120ms] ease-out data-[state=checked]:translate-x-4" />
    </Primitive.Root>
  );
}
