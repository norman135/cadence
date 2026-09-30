import { Check, ChevronRight } from 'lucide-react';
import { DropdownMenu as Primitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export const DropdownMenu = Primitive.Root;
export const DropdownMenuTrigger = Primitive.Trigger;
export const DropdownMenuGroup = Primitive.Group;
export const DropdownMenuSub = Primitive.Sub;
export const DropdownMenuRadioGroup = Primitive.RadioGroup;

const content =
  'z-50 min-w-52 animate-pop-in overflow-hidden rounded-lg border bg-popover p-1.5 text-popover-foreground shadow-lg';

const item =
  "relative flex h-8 cursor-default items-center gap-2.5 rounded-md px-2 text-sm outline-none select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50 data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4 [&_svg:not([class*='text-'])]:text-muted-foreground";

export function DropdownMenuContent({
  className,
  sideOffset = 6,
  ...props
}: ComponentProps<typeof Primitive.Content>) {
  return (
    <Primitive.Portal>
      <Primitive.Content sideOffset={sideOffset} className={cn(content, className)} {...props} />
    </Primitive.Portal>
  );
}

export function DropdownMenuItem({
  className,
  variant = 'default',
  ...props
}: ComponentProps<typeof Primitive.Item> & { variant?: 'default' | 'destructive' }) {
  return (
    <Primitive.Item
      className={cn(
        item,
        variant === 'destructive' &&
          'text-destructive data-[highlighted]:text-destructive [&_svg:not([class*=text-])]:text-destructive',
        className,
      )}
      {...props}
    />
  );
}

/** One choice in a DropdownMenuRadioGroup; the selected one shows a check. */
export function DropdownMenuRadioItem({
  className,
  children,
  ...props
}: ComponentProps<typeof Primitive.RadioItem>) {
  return (
    <Primitive.RadioItem className={cn(item, 'pr-8', className)} {...props}>
      {children}
      <Primitive.ItemIndicator className="absolute right-2 flex">
        <Check className="size-4 text-primary" />
      </Primitive.ItemIndicator>
    </Primitive.RadioItem>
  );
}

export function DropdownMenuSubTrigger({
  className,
  children,
  ...props
}: ComponentProps<typeof Primitive.SubTrigger>) {
  return (
    <Primitive.SubTrigger className={cn(item, 'data-[state=open]:bg-accent', className)} {...props}>
      {children}
      <ChevronRight className="ml-auto size-4" />
    </Primitive.SubTrigger>
  );
}

export function DropdownMenuSubContent({
  className,
  ...props
}: ComponentProps<typeof Primitive.SubContent>) {
  return (
    <Primitive.Portal>
      <Primitive.SubContent
        sideOffset={8}
        className={cn(content, 'min-w-40', className)}
        {...props}
      />
    </Primitive.Portal>
  );
}

export function DropdownMenuLabel({ className, ...props }: ComponentProps<typeof Primitive.Label>) {
  return (
    <Primitive.Label
      className={cn('px-2 pt-1.5 pb-1 text-xs font-medium text-subtle-foreground', className)}
      {...props}
    />
  );
}

export function DropdownMenuSeparator({
  className,
  ...props
}: ComponentProps<typeof Primitive.Separator>) {
  return (
    <Primitive.Separator className={cn('-mx-1.5 my-1.5 h-px bg-border', className)} {...props} />
  );
}
