import { Toaster as Sonner } from 'sonner';
import { useTheme } from '@/shared/theme';

/** Toasts styled to the design: a raised card, the icon carries the signal color. */
export function Toaster() {
  const { resolved } = useTheme();

  return (
    <Sonner
      theme={resolved}
      position="bottom-right"
      closeButton
      toastOptions={{
        unstyled: true,
        classNames: {
          toast:
            'flex w-[356px] items-start gap-2.5 rounded-lg border bg-popover px-3.5 py-3 text-sm text-popover-foreground shadow-lg',
          title: 'font-medium',
          description: 'text-[13px] text-muted-foreground',
          icon: 'mt-px [&_svg]:size-[18px]',
          success: '[&_[data-icon]]:text-success',
          error: '[&_[data-icon]]:text-destructive',
          warning: '[&_[data-icon]]:text-warning',
          info: '[&_[data-icon]]:text-info',
          actionButton:
            'ml-auto h-7 shrink-0 rounded-[7px] px-2.5 text-[13px] font-medium text-muted-foreground hover:bg-accent hover:text-foreground',
          closeButton:
            'absolute -top-2 -left-2 grid size-5 place-items-center rounded-full border bg-popover text-muted-foreground [&_svg]:size-3',
        },
      }}
    />
  );
}
