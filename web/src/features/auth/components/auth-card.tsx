import type { ReactNode } from 'react';

/**
 * The frame shared by every account page: a heading, then the form. The layout supplies the
 * surrounding page (design board 10), so there is no card; inputs and the submit button use the
 * 40 px touch size.
 */
export function AuthCard({
  title,
  description,
  children,
  footer,
}: {
  title: string;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <div className="flex w-full flex-col gap-7">
      <div className="flex flex-col gap-1.5">
        <h1 className="text-[28px] leading-tight font-semibold tracking-[-0.025em]">{title}</h1>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      <div className="flex flex-col gap-6 [&_button[type=submit]]:h-10 [&_button[type=submit]]:w-full [&_button[type=submit]]:text-[15px] [&_input]:h-10 [&_input]:px-3">
        {children}
      </div>
      {footer && <div className="text-center text-sm text-muted-foreground">{footer}</div>}
    </div>
  );
}
