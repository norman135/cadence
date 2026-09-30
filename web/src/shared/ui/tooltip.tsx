import { Tooltip as Primitive } from 'radix-ui';
import type { ComponentProps, ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

/** Mount once near the root; it shares the open delay between tooltips. */
export function TooltipProvider(props: ComponentProps<typeof Primitive.Provider>) {
  return <Primitive.Provider delayDuration={400} skipDelayDuration={200} {...props} />;
}

/**
 * A short label for an icon-only control. Tooltips never hold the only copy of important
 * information: the control also needs an aria-label.
 */
export function Tooltip({
  content,
  children,
  side = 'bottom',
  className,
}: {
  content: ReactNode;
  children: ReactNode;
  side?: ComponentProps<typeof Primitive.Content>['side'];
  className?: string;
}) {
  return (
    <Primitive.Root>
      <Primitive.Trigger asChild>{children}</Primitive.Trigger>
      <Primitive.Portal>
        <Primitive.Content
          side={side}
          sideOffset={6}
          className={cn(
            'z-50 inline-flex animate-fade-in items-center gap-2 rounded-[7px] bg-foreground px-2 py-1 text-xs font-medium text-background',
            className,
          )}
        >
          {content}
        </Primitive.Content>
      </Primitive.Portal>
    </Primitive.Root>
  );
}
