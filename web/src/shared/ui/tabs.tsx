import { Tabs as Primitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export const Tabs = Primitive.Root;
export const TabsContent = Primitive.Content;

/** A segmented control: the active tab lifts onto a card surface. */
export function TabsList({ className, ...props }: ComponentProps<typeof Primitive.List>) {
  return (
    <Primitive.List
      className={cn('inline-flex gap-0.5 rounded-[9px] border bg-sunken p-[3px]', className)}
      {...props}
    />
  );
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof Primitive.Trigger>) {
  return (
    <Primitive.Trigger
      className={cn(
        "inline-flex h-[26px] cursor-default items-center gap-1.5 rounded-md px-2.5 text-[13px] font-medium text-muted-foreground transition-colors duration-[120ms] outline-none hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring data-[state=active]:bg-card data-[state=active]:text-foreground data-[state=active]:shadow-sm dark:data-[state=active]:bg-muted [&_svg:not([class*='size-'])]:size-3.5",
        className,
      )}
      {...props}
    />
  );
}
