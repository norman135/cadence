import { Check } from 'lucide-react';
import { Checkbox as Primitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export function Checkbox({ className, ...props }: ComponentProps<typeof Primitive.Root>) {
  return (
    <Primitive.Root
      className={cn(
        'inline-grid size-4 shrink-0 cursor-default place-items-center rounded-[5px] border border-input bg-card transition-colors duration-[120ms] outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:opacity-50 data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground',
        className,
      )}
      {...props}
    >
      <Primitive.Indicator>
        <Check className="size-3" strokeWidth={3} />
      </Primitive.Indicator>
    </Primitive.Root>
  );
}
